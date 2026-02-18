using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HVG2020B.Core;
using HVG2020B.Core.Models;
using HVG2020B.Core.Services;
using HVG2020B.Driver;
using HVG2020B.Viewer.Services;

namespace HVG2020B.Viewer.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly DeviceManager _deviceManager;
    private readonly HashSet<string> _managedDeviceIds = new();
    private readonly Dictionary<string, DeviceSeries> _seriesByDevice = new();
    private readonly ObservableCollection<DeviceSeries> _deviceSeries = new();
    private readonly List<string> _seriesPalette = new()
    {
        "#0076C0",
        "#4CAF50",
        "#FF9800",
        "#9C27B0",
        "#E53935",
        "#00BCD4"
    };
    private int _seriesPaletteIndex;

    private readonly List<double> _timeData = new();
    private readonly List<double> _pressureData = new();
    private DateTime _startTime;
    private DateTime _recordingStartTime;
    private int _sampleCount;
    private bool _disposed;

    // Per-study recording (multiple simultaneous)
    private readonly List<StudyItem> _recordingStudies = new();

    // Study persistence
    private readonly string _logDir;
    private readonly StudyStore _studyStore;

    private const int LiveIntervalMs = 100;

    // Logging tick threshold
    private const int LogTickThreshold = 5; // 500ms / 100ms = 5 ticks

    // Blinking indicator timer
    private readonly DispatcherTimer _blinkTimer;
    private readonly DispatcherTimer _scanTimer;
    private CancellationTokenSource? _scanCts;

    // Minimum pressure value for log scale (avoids log(0) issues)
    private const double MinPressureForLog = 1e-12;

    public MainViewModel()
    {
        _deviceManager = new DeviceManager(TimeSpan.FromMilliseconds(LiveIntervalMs));
        _deviceManager.ReadingReceived += OnReadingReceived;
        _deviceManager.ConnectionLost += OnDeviceConnectionLost;

        // Setup blink timer for recording indicator
        _blinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _blinkTimer.Tick += (_, _) =>
        {
            if (CurrentState == ViewerState.Recording)
            {
                IndicatorVisible = !IndicatorVisible;
            }
        };

        _scanTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _scanTimer.Tick += (_, _) =>
        {
            if (IsScanning)
            {
                ScanElapsedSeconds++;
            }
        };

        // Study persistence
        _logDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        _studyStore = new StudyStore(_logDir);
        foreach (var record in _studyStore.LoadAll())
        {
            Studies.Add(new StudyItem(record));
        }
        ApplyStudyFilter();
        if (FilteredStudies.Count > 0)
            SelectedStudy = FilteredStudies[0];
        RefreshFluxResultsDashboard();
    }

    #region Observable Properties

    [ObservableProperty]
    private ObservableCollection<DeviceItem> _devices = new();

    [ObservableProperty]
    private DeviceItem? _selectedDevice;

    [ObservableProperty]
    private ViewerState _currentState = ViewerState.Disconnected;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private ObservableCollection<StudyItem> _studies = new();

    [ObservableProperty]
    private ObservableCollection<StudyItem> _filteredStudies = new();

    [ObservableProperty]
    private string _studySearchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedStudy))]
    private StudyItem? _selectedStudy;

    [ObservableProperty]
    private bool _isNewStudyDialogOpen;

    [ObservableProperty]
    private string _newStudyTitle = string.Empty;

    [ObservableProperty]
    private string _newStudyId = string.Empty;

    [ObservableProperty]
    private ObservableCollection<NewStudyDeviceOption> _newStudyDeviceSelections = new();

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanProgressText = string.Empty;

    [ObservableProperty]
    private double _scanProgressValue;

    [ObservableProperty]
    private int _scanElapsedSeconds;

    [ObservableProperty]
    private bool _hasScanResults;

    public ObservableCollection<ScannedDevice> ScanResults { get; } = new();

    [ObservableProperty]
    private int _sampleCountDisplay;

    [ObservableProperty]
    private int _recordedSampleCountDisplay;

    [ObservableProperty]
    private string _elapsedTime = "00:00:00";

    [ObservableProperty]
    private string _recordingTime = "00:00:00";

    [ObservableProperty]
    private bool _indicatorVisible = true;

    [ObservableProperty]
    private string? _logFilePath;

    /// <summary>
    /// Y-axis scale type: true = Log, false = Linear
    /// </summary>
    [ObservableProperty]
    private bool _useLogScale = true;

    [ObservableProperty]
    private ObservableCollection<FluxAnalysisResult> _allFluxResults = new();

    [ObservableProperty]
    private FluxAnalysisResult? _selectedFluxResult;

    #endregion

    #region Computed Properties

    public bool IsDisconnected => CurrentState == ViewerState.Disconnected;
    public bool IsLive => CurrentState == ViewerState.Live;
    public bool IsRecording => CurrentState == ViewerState.Recording;
    public bool IsConnected => CurrentState != ViewerState.Disconnected;
    public bool HasSelectedStudy => SelectedStudy != null;

    public ObservableCollection<DeviceSeries> DeviceSeries => _deviceSeries;

    #endregion

    #region Chart Data

    public List<double> TimeData => _timeData;
    public List<double> PressureData => _pressureData;

    public event Action? DataUpdated;
    public event Action? ScaleChanged;

    #endregion

    #region State Change Handler

    partial void OnCurrentStateChanged(ViewerState value)
    {
        OnPropertyChanged(nameof(IsDisconnected));
        OnPropertyChanged(nameof(IsLive));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsConnected));

        // Handle blink timer
        if (value == ViewerState.Recording)
        {
            _blinkTimer.Start();
        }
        else
        {
            _blinkTimer.Stop();
            IndicatorVisible = true;
        }
    }

    partial void OnUseLogScaleChanged(bool value)
    {
        ScaleChanged?.Invoke();
    }

    partial void OnSelectedDeviceChanged(DeviceItem? value)
    {
        UpdateSelectedSeriesData(value?.DeviceId);
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task ScanForDevices()
    {
        if (IsScanning)
        {
            return;
        }

        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();

        IsScanning = true;
        HasScanResults = false;
        ScanProgressValue = 0;
        ScanElapsedSeconds = 0;
        ScanProgressText = "Starting scan...";
        ScanResults.Clear();
        _scanTimer.Start();

        var token = _scanCts.Token;
        try
        {
            var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
            var connectedPorts = Devices
                .Where(d => d.IsConnected && !string.IsNullOrWhiteSpace(d.PortName))
                .Select(d => d.PortName!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var portsToScan = ports.Where(p => !connectedPorts.Contains(p)).ToArray();
            if (portsToScan.Length == 0)
            {
                ScanProgressText = "No available ports to scan";
                HasScanResults = true;
                return;
            }

            for (var i = 0; i < portsToScan.Length; i++)
            {
                token.ThrowIfCancellationRequested();

                var port = portsToScan[i];
                ScanProgressText = $"{port} 시도 중 ({i + 1}/{portsToScan.Length})";
                ScanProgressValue = (double)(i + 1) / portsToScan.Length;

                var client = new HVG2020BClient();
                try
                {
                    await client.ConnectWithAutoScanAsync(port, token);

                    ScanResults.Add(new ScannedDevice
                    {
                        DeviceId = client.DeviceId,
                        PortName = port,
                        DeviceType = client.DeviceType,
                        Device = client,
                        IsAdded = false
                    });
                }
                catch (OperationCanceledException)
                {
                    client.Dispose();
                    throw;
                }
                catch
                {
                    client.Dispose();
                }
            }

            HasScanResults = true;
            ScanProgressText = ScanResults.Count > 0
                ? $"✅ {ScanResults.Count} devices found"
                : "No devices found";
        }
        catch (OperationCanceledException)
        {
            ScanProgressText = "Scan cancelled";
        }
        finally
        {
            IsScanning = false;
            _scanTimer.Stop();
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _scanCts?.Cancel();
    }

    [RelayCommand]
    private async Task AddScannedDevice(ScannedDevice device)
    {
        if (device.IsAdded)
        {
            return;
        }

        IGaugeDevice deviceToAdd = device.Device;
        var replacedDevice = false;
        if (!deviceToAdd.IsConnected)
        {
            try
            {
                var reconnectClient = new HVG2020BClient(device.DeviceId);
                await reconnectClient.ConnectWithAutoScanAsync(device.PortName);
                deviceToAdd = reconnectClient;
                replacedDevice = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Reconnect failed: {ex.Message}";
                return;
            }
        }

        var item = new DeviceItem(deviceToAdd);
        item.IsConnected = deviceToAdd.IsConnected;
        item.PortName = device.PortName;

        Devices.Add(item);
        item.PropertyChanged += OnDeviceItemPropertyChanged;
        EnsureDeviceSeries(item.DeviceId);

        if (SelectedDevice == null)
        {
            SelectedDevice = item;
        }

        if (replacedDevice)
        {
            device.Device.Dispose();
        }

        device.IsAdded = true;

        if (_managedDeviceIds.Add(item.DeviceId))
        {
            _deviceManager.AddDevice(item.Device);
        }

        if (CurrentState == ViewerState.Disconnected)
        {
            CurrentState = ViewerState.Live;
            _startTime = DateTime.Now;
            _sampleCount = 0;
        }

        StatusMessage = $"Device added: {item.DeviceId}";

        if (ScanResults.All(r => r.IsAdded))
        {
            HasScanResults = false;
        }
    }

    [RelayCommand]
    private void AddEmulatorDevice()
    {
        var emulator = new EmulatorDevice();
        emulator.ConnectAsync("EMULATOR").GetAwaiter().GetResult();

        var item = new DeviceItem(emulator)
        {
            IsConnected = true,
            PortName = "EMULATOR"
        };

        Devices.Add(item);
        item.PropertyChanged += OnDeviceItemPropertyChanged;
        EnsureDeviceSeries(item.DeviceId);

        SelectedDevice ??= item;

        if (_managedDeviceIds.Add(item.DeviceId))
        {
            _deviceManager.AddDevice(item.Device);
        }

        if (CurrentState == ViewerState.Disconnected)
        {
            CurrentState = ViewerState.Live;
            _startTime = DateTime.Now;
            _sampleCount = 0;
        }

        StatusMessage = $"Emulator added: {item.DeviceId}";
    }

    [RelayCommand]
    private void RemoveDevice(DeviceItem device)
    {
        if (device == null)
        {
            return;
        }

        device.Device.Disconnect();
        device.IsConnected = false;
        device.PortName = null;
        device.CurrentPressure = "---";

        _deviceManager.RemoveDevice(device.DeviceId);
        _managedDeviceIds.Remove(device.DeviceId);

        if (_seriesByDevice.TryGetValue(device.DeviceId, out var series))
        {
            _seriesByDevice.Remove(device.DeviceId);
            _deviceSeries.Remove(series);
        }

        Devices.Remove(device);
        device.PropertyChanged -= OnDeviceItemPropertyChanged;

        UpdateCurrentStateFromDevices();
        DataUpdated?.Invoke();
    }

    [RelayCommand]
    private void ToggleGraph(DeviceItem device)
    {
        if (device == null)
        {
            return;
        }

        device.IsVisibleOnChart = !device.IsVisibleOnChart;
    }

    [RelayCommand]
    private void OpenNewStudyDialog()
    {
        IsNewStudyDialogOpen = true;
        NewStudyTitle = string.Empty;
        NewStudyDeviceSelections.Clear();

        foreach (var device in Devices.Where(d => d.IsConnected))
        {
            NewStudyDeviceSelections.Add(new NewStudyDeviceOption(device.DeviceId, isSelected: true));
        }

        if (NewStudyDeviceSelections.Count == 0)
        {
            StatusMessage = "No connected devices available for study";
        }
    }

    [RelayCommand]
    private void SelectAllDevices()
    {
        foreach (var d in NewStudyDeviceSelections) d.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAllDevices()
    {
        foreach (var d in NewStudyDeviceSelections) d.IsSelected = false;
    }

    [RelayCommand]
    private void CancelNewStudy()
    {
        IsNewStudyDialogOpen = false;
        NewStudyTitle = string.Empty;
        NewStudyId = string.Empty;
        NewStudyDeviceSelections.Clear();
    }

    [RelayCommand]
    private void CreateNewStudy()
    {
        if (string.IsNullOrWhiteSpace(NewStudyId))
        {
            StatusMessage = "Please enter an Id";
            return;
        }

        var selectedDevices = NewStudyDeviceSelections
            .Where(d => d.IsSelected)
            .Select(d => d.DeviceId)
            .ToList();

        if (selectedDevices.Count == 0)
        {
            StatusMessage = "Please select at least one device";
            return;
        }

        var metadata = new StudyMetadata
        {
            StudyId = GenerateStudyId(),
            Title = NewStudyTitle.Trim(),
            MeasurementId = NewStudyId.Trim(),
            CreatedAt = DateTimeOffset.Now,
            DeviceIds = selectedDevices
        };

        var newStudy = new StudyItem(metadata);
        Studies.Insert(0, newStudy);
        ApplyStudyFilter();
        SelectedStudy = newStudy;
        PersistStudies();

        IsNewStudyDialogOpen = false;
        NewStudyTitle = string.Empty;
        NewStudyId = string.Empty;
        NewStudyDeviceSelections.Clear();

        StatusMessage = $"Study created: {metadata.StudyId}";
    }

    [RelayCommand]
    private void StartStudyRecording(object? parameter)
    {
        if (parameter is not StudyItem study) return;
        if (study.State != StudyState.Ready) return;

        if (CurrentState == ViewerState.Disconnected)
        {
            StatusMessage = "Cannot record: no devices connected";
            return;
        }

        try
        {
            study.StartRecording(_logDir);
            _recordingStudies.Add(study);
            PersistStudies();

            if (CurrentState != ViewerState.Recording)
            {
                CurrentState = ViewerState.Recording;
                _recordingStartTime = DateTime.Now;
                RecordingTime = "00:00:00";
            }

            StatusMessage = $"Recording '{study.Title}' ({_recordingStudies.Count} active)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start recording: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StopStudyRecording(object? parameter)
    {
        if (parameter is not StudyItem study) return;
        if (study.State != StudyState.Recording) return;

        study.StopRecording();
        _recordingStudies.Remove(study);
        PersistStudies();

        StatusMessage = $"Stopped '{study.Title}' - {study.RecordedSampleCount} samples";

        if (_recordingStudies.Count == 0)
        {
            CurrentState = ViewerState.Live;
        }
    }

    [RelayCommand]
    private void DeleteStudy(object? parameter)
    {
        if (parameter is not StudyItem study) return;

        if (study.State == StudyState.Recording)
        {
            StopStudyRecording(study);
        }

        // Ask user whether to also delete files from disk
        var folderPath = study.StudyFolderPath;
        bool deleteFiles = false;
        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
        {
            var result = MessageBox.Show(
                $"Also delete study files from disk?\n\n{folderPath}",
                "Delete Study",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return;

            deleteFiles = result == MessageBoxResult.Yes;
        }

        study.Dispose();
        Studies.Remove(study);
        ApplyStudyFilter();
        PersistStudies();
        RefreshFluxResultsDashboard();

        if (deleteFiles && !string.IsNullOrEmpty(folderPath))
        {
            try
            {
                Directory.Delete(folderPath, recursive: true);
                StatusMessage = $"Study deleted (files removed): {study.StudyId}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Study deleted, but folder removal failed: {ex.Message}";
            }
        }
        else
        {
            StatusMessage = $"Study deleted: {study.StudyId}";
        }
    }

    [RelayCommand]
    private void AnalyzeStudy(object? parameter)
    {
        if (parameter is not StudyItem study) return;
        if (study.State != StudyState.Done) return;
        if (string.IsNullOrEmpty(study.CsvFilePath) || !File.Exists(study.CsvFilePath))
        {
            StatusMessage = $"CSV file not found for study '{study.Title}'";
            return;
        }

        try
        {
            var parsed = StudyCsvParser.Parse(study.CsvFilePath);

            if (parsed.DeviceIds.Count == 0)
            {
                StatusMessage = "No data found in study CSV";
                return;
            }

            string selectedDeviceId;
            if (parsed.DeviceIds.Count == 1)
            {
                selectedDeviceId = parsed.DeviceIds[0];
            }
            else
            {
                var dialog = new DeviceSelectionDialog(parsed.DeviceIds);
                dialog.Owner = Application.Current.MainWindow;
                if (dialog.ShowDialog() != true || dialog.SelectedDeviceId == null)
                    return;
                selectedDeviceId = dialog.SelectedDeviceId;
            }

            var (timeSeconds, pressureTorr) = StudyCsvParser.ExtractDeviceData(
                parsed.RowsByDevice[selectedDeviceId]);

            if (timeSeconds.Length < 2)
            {
                StatusMessage = $"Insufficient data for device {selectedDeviceId} ({timeSeconds.Length} points)";
                return;
            }

            var studyFolder = study.StudyFolderPath ?? Path.GetDirectoryName(study.CsvFilePath);
            var fluxWindow = new FluxCalculationWindow(
                new List<double>(timeSeconds), new List<double>(pressureTorr), studyFolder);
            fluxWindow.Title = $"Flux Calculation - {study.Title} ({selectedDeviceId})";
            fluxWindow.Owner = Application.Current.MainWindow;
            fluxWindow.ShowDialog();

            if (fluxWindow.CalculationResult is { } calc)
            {
                // Capture screenshot before closing window
                string? screenshotPath = null;
                if (studyFolder != null)
                {
                    try
                    {
                        screenshotPath = ScreenshotCapture.CaptureWindow(
                            fluxWindow, studyFolder,
                            $"{study.StudyId}_analysis_{study.Metadata.LatestAnalysisId + 1}");
                    }
                    catch { /* screenshot failure is non-fatal */ }
                }

                var analysisResult = new FluxAnalysisResult
                {
                    CalculatedAt = DateTimeOffset.Now,
                    StudyId = study.StudyId,
                    StudyTitle = study.Title,
                    MeasurementId = study.MeasurementId,
                    DeviceId = selectedDeviceId,
                    ScreenshotPath = screenshotPath,
                    MembraneArea = calc.MembraneArea,
                    Temperature = calc.Temperature,
                    FeedSidePressure = calc.FeedSidePressure,
                    ChamberVolume = calc.ChamberVolume,
                    StartTime = calc.StartTime,
                    EndTime = calc.EndTime,
                    Flux = calc.Flux,
                    Permeance = calc.Permeance,
                    PermeanceGpu = calc.PermeanceGpu,
                    PressureChangeRate = calc.PressureChangeRate,
                    RSquared = calc.RSquared,
                    DataPointCount = calc.DataPointCount
                };

                study.AddAnalysisResult(analysisResult);
                PersistStudies();
                RefreshFluxResultsDashboard();

                // Auto-export Excel to study folder
                if (studyFolder != null && study.CsvFilePath != null)
                {
                    try
                    {
                        var excelPath = Path.Combine(studyFolder,
                            $"{SanitizeFolderName(study.Title)}.xlsx");
                        StudyExcelExporter.Export(excelPath, study.CsvFilePath,
                            study.AnalysisResults, study.Metadata);
                        StatusMessage = $"Analysis #{analysisResult.AnalysisId} saved + Excel exported";
                    }
                    catch (Exception excelEx)
                    {
                        StatusMessage = $"Analysis saved, Excel export failed: {excelEx.Message}";
                    }
                }
                else
                {
                    StatusMessage = $"Analysis saved: {study.Title} #{analysisResult.AnalysisId}";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Analysis failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AnalyzeSelectedStudy()
    {
        if (SelectedStudy != null)
            AnalyzeStudy(SelectedStudy);
    }

    [RelayCommand]
    private void DeleteSelectedStudy()
    {
        if (SelectedStudy != null)
            DeleteStudy(SelectedStudy);
    }

    [RelayCommand]
    private void OpenStudyFolder()
    {
        if (SelectedStudy?.StudyFolderPath == null) return;
        var folderPath = SelectedStudy.StudyFolderPath;
        if (!Directory.Exists(folderPath))
        {
            StatusMessage = $"Folder not found: {folderPath}";
            return;
        }

        try
        {
            System.Diagnostics.Process.Start("explorer.exe", folderPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearChart()
    {
        foreach (var series in _deviceSeries)
        {
            series.TimeData.Clear();
            series.PressureData.Clear();
            series.StartTime = default;
        }

        _timeData.Clear();
        _pressureData.Clear();
        _sampleCount = 0;
        SampleCountDisplay = 0;
        _startTime = DateTime.Now;
        ElapsedTime = "00:00:00";
        DataUpdated?.Invoke();
    }

    #endregion

    #region Device Readings

    private void OnReadingReceived(object? sender, (string DeviceId, GaugeReading Reading) payload)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => HandleReading(payload.DeviceId, payload.Reading));
            return;
        }

        HandleReading(payload.DeviceId, payload.Reading);
    }

    private void HandleReading(string deviceId, GaugeReading reading)
    {
        var deviceItem = Devices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (deviceItem == null)
        {
            return;
        }

        _sampleCount++;
        SampleCountDisplay = _sampleCount;

        // Update device display
        deviceItem.CurrentPressure = $"{reading.PressureTorr:E3} Torr";
        deviceItem.IsConnected = true;
        deviceItem.PortName = deviceItem.Device.PortName;

        // Update elapsed time (since first connection)
        var elapsed = DateTime.Now - _startTime;
        ElapsedTime = elapsed.ToString(@"hh\:mm\:ss");

        // Add to chart data (per device)
        var series = EnsureDeviceSeries(deviceId);
        if (series.StartTime == default)
        {
            series.StartTime = reading.Timestamp;
        }

        var timeSeconds = (reading.Timestamp - series.StartTime).TotalSeconds;
        series.TimeData.Add(timeSeconds);
        var pressure = Math.Max(reading.PressureTorr, MinPressureForLog);
        series.PressureData.Add(pressure);

        // Update selected series buffer for existing chart binding
        if (SelectedDevice?.DeviceId == deviceId)
        {
            UpdateSelectedSeriesData(deviceId);
        }

        // Per-study recording (multiple simultaneous)
        if (_recordingStudies.Count > 0)
        {
            var totalWritten = 0;
            foreach (var activeStudy in _recordingStudies)
            {
                if (activeStudy.TryWriteReading(deviceId, reading, LogTickThreshold))
                    totalWritten++;
            }

            if (totalWritten > 0)
            {
                var totalSamples = _recordingStudies.Sum(s => s.RecordedSampleCount);
                RecordedSampleCountDisplay = totalSamples;
                StatusMessage = $"Recording {_recordingStudies.Count} studies - {totalSamples} total samples";
            }

            var recordingElapsed = DateTime.Now - _recordingStartTime;
            RecordingTime = recordingElapsed.ToString(@"hh\:mm\:ss");
        }

        DataUpdated?.Invoke();

        if (CurrentState == ViewerState.Live)
        {
            StatusMessage = $"Live - {_sampleCount} samples";
        }
    }

    #endregion

    private void OnDeviceConnectionLost(object? sender, (string DeviceId, Exception Error) payload)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => OnDeviceConnectionLost(sender, payload));
            return;
        }

        var deviceItem = Devices.FirstOrDefault(d => d.DeviceId == payload.DeviceId);
        if (deviceItem != null)
        {
            deviceItem.IsConnected = false;
            deviceItem.CurrentPressure = "---";
        }

        StatusMessage = $"Connection lost ({payload.DeviceId}): {payload.Error.Message}";
        UpdateCurrentStateFromDevices();
    }

    private void OnDeviceItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeviceItem.IsVisibleOnChart))
        {
            DataUpdated?.Invoke();
        }
    }

    private DeviceSeries EnsureDeviceSeries(string deviceId)
    {
        if (_seriesByDevice.TryGetValue(deviceId, out var existing))
        {
            return existing;
        }

        var series = new DeviceSeries(deviceId, GetNextSeriesColor());
        _seriesByDevice[deviceId] = series;
        _deviceSeries.Add(series);
        return series;
    }

    private string GetNextSeriesColor()
    {
        var color = _seriesPalette[_seriesPaletteIndex % _seriesPalette.Count];
        _seriesPaletteIndex++;
        return color;
    }

    private void UpdateSelectedSeriesData(string? deviceId)
    {
        _timeData.Clear();
        _pressureData.Clear();

        if (deviceId == null)
        {
            DataUpdated?.Invoke();
            return;
        }

        if (_seriesByDevice.TryGetValue(deviceId, out var series))
        {
            _timeData.AddRange(series.TimeData);
            _pressureData.AddRange(series.PressureData);
        }

        DataUpdated?.Invoke();
    }

    private void UpdateCurrentStateFromDevices()
    {
        if (Devices.Any(d => d.IsConnected))
        {
            if (CurrentState == ViewerState.Recording)
            {
                return;
            }

            CurrentState = ViewerState.Live;
            return;
        }

        CurrentState = ViewerState.Disconnected;
    }

    private static string GenerateStudyId()
    {
        var now = DateTime.Now;
        return $"STD-{now:yyyyMMdd}-{now:HHmmss}";
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Untitled" : sanitized.Trim();
    }

    partial void OnStudySearchTextChanged(string value)
    {
        ApplyStudyFilter();
    }

    private void ApplyStudyFilter()
    {
        var search = StudySearchText?.Trim() ?? "";
        FilteredStudies.Clear();
        foreach (var study in Studies)
        {
            if (string.IsNullOrEmpty(search)
                || study.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || study.StudyId.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                FilteredStudies.Add(study);
            }
        }
    }

    private void PersistStudies()
    {
        try
        {
            _studyStore.SaveAll(Studies.Select(s => s.ToRecord()));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save studies: {ex.Message}";
        }
    }

    private void RefreshFluxResultsDashboard()
    {
        AllFluxResults.Clear();
        foreach (var study in Studies)
        {
            foreach (var result in study.AnalysisResults)
            {
                AllFluxResults.Add(result);
            }
        }
    }

    [RelayCommand]
    private void OpenFluxResultStudy(FluxAnalysisResult? result)
    {
        if (result == null || string.IsNullOrEmpty(result.StudyId))
        {
            StatusMessage = "Cannot navigate: no Study ID associated with this result";
            return;
        }

        var study = Studies.FirstOrDefault(s => s.StudyId == result.StudyId);
        if (study == null)
        {
            StatusMessage = $"Study not found: {result.StudyId}";
            return;
        }

        AnalyzeStudy(study);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _blinkTimer.Stop();
        _scanTimer.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();

        foreach (var device in Devices)
        {
            device.PropertyChanged -= OnDeviceItemPropertyChanged;
        }

        foreach (var recording in _recordingStudies)
        {
            recording.StopRecording();
        }
        _recordingStudies.Clear();

        PersistStudies();

        foreach (var study in Studies)
        {
            study.Dispose();
        }

        _deviceManager.ReadingReceived -= OnReadingReceived;
        _deviceManager.ConnectionLost -= OnDeviceConnectionLost;
        _deviceManager.Dispose();
    }
}

public sealed partial class DeviceItem : ObservableObject
{
    public DeviceItem(IGaugeDevice device)
    {
        Device = device;
        DeviceId = device.DeviceId;
        CurrentPressure = "---";
    }

    public IGaugeDevice Device { get; }

    public string DeviceId { get; }

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isVisibleOnChart = true;

    [ObservableProperty]
    private string? _portName;

    [ObservableProperty]
    private string _currentPressure;
}

public sealed class DeviceSeries
{
    public DeviceSeries(string deviceId, string colorHex)
    {
        DeviceId = deviceId;
        ColorHex = colorHex;
    }

    public string DeviceId { get; }

    public string ColorHex { get; }

    public List<double> TimeData { get; } = new();

    public List<double> PressureData { get; } = new();

    public DateTimeOffset StartTime { get; set; }
}

public sealed partial class NewStudyDeviceOption : ObservableObject
{
    public NewStudyDeviceOption(string deviceId, bool isSelected)
    {
        DeviceId = deviceId;
        _isSelected = isSelected;
    }

    public string DeviceId { get; }

    [ObservableProperty]
    private bool _isSelected;
}
