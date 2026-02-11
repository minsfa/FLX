using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HVG2020B.Core;
using HVG2020B.Core.Models;
using HVG2020B.Driver;

namespace HVG2020B.Viewer.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly DeviceManager _deviceManager;
    private readonly HashSet<string> _managedDeviceIds = new();
    private readonly Dictionary<string, DeviceSeries> _seriesByDevice = new();
    private readonly ObservableCollection<DeviceSeries> _deviceSeries = new();
    private readonly Dictionary<string, int> _logTickCounters = new();
    private readonly List<string> _seriesPalette = new()
    {
        "#14427B",
        "#1F77B4",
        "#2CA02C",
        "#FF7F0E",
        "#D62728",
        "#9467BD"
    };
    private int _seriesPaletteIndex;

    private readonly List<double> _timeData = new();
    private readonly List<double> _pressureData = new();
    private DateTime _startTime;
    private DateTime _recordingStartTime;
    private int _sampleCount;
    private int _recordedSampleCount;
    private bool _disposed;

    // CSV logging
    private StreamWriter? _csvWriter;
    private string? _currentLogPath;

    private const int LiveIntervalMs = 100;

    // Logging tick threshold
    private int _logTickThreshold = 5; // 500ms / 100ms = 5 ticks
    private int _loggingIntervalMs = 500;

    // Blinking indicator timer
    private readonly DispatcherTimer _blinkTimer;
    private readonly DispatcherTimer _scanTimer;
    private CancellationTokenSource? _scanCts;

    // Ring buffer for chart (last N seconds at 10Hz = 600 points = 60 seconds)
    private const int MaxDataPoints = 600;

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
    private bool _isNewStudyDialogOpen;

    [ObservableProperty]
    private string _newStudyTitle = string.Empty;

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

    #endregion

    #region Computed Properties

    public bool IsDisconnected => CurrentState == ViewerState.Disconnected;
    public bool IsLive => CurrentState == ViewerState.Live;
    public bool IsRecording => CurrentState == ViewerState.Recording;
    public bool IsConnected => CurrentState != ViewerState.Disconnected;

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
            _recordedSampleCount = 0;
        }

        StatusMessage = $"Device added: {item.DeviceId}";

        if (ScanResults.All(r => r.IsAdded))
        {
            HasScanResults = false;
        }
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
        _logTickCounters.Remove(device.DeviceId);

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
    private void CancelNewStudy()
    {
        IsNewStudyDialogOpen = false;
        NewStudyTitle = string.Empty;
        NewStudyDeviceSelections.Clear();
    }

    [RelayCommand]
    private void CreateNewStudy()
    {
        if (string.IsNullOrWhiteSpace(NewStudyTitle))
        {
            StatusMessage = "Please enter a study title";
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
            CreatedAt = DateTimeOffset.Now,
            DeviceIds = selectedDevices
        };

        Studies.Insert(0, new StudyItem(metadata));

        IsNewStudyDialogOpen = false;
        NewStudyTitle = string.Empty;
        NewStudyDeviceSelections.Clear();

        StatusMessage = $"Study created: {metadata.StudyId}";
    }

    /// <summary>
    /// Start recording to CSV file.
    /// </summary>
    private void StartRecording()
    {
        try
        {
            // Create log directory if needed
            var logDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            // Create new log file
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentLogPath = Path.Combine(logDir, $"hvg_2020b_{timestamp}.csv");
            _csvWriter = new StreamWriter(_currentLogPath, false, System.Text.Encoding.UTF8);
            _csvWriter.WriteLine("timestamp_iso,pressure_torr");

            LogFilePath = _currentLogPath;
            _recordingStartTime = DateTime.Now;
            _recordedSampleCount = 0;
            RecordedSampleCountDisplay = 0;
            RecordingTime = "00:00:00";
            _logTickCounters.Clear();

            CurrentState = ViewerState.Recording;
            StatusMessage = $"Recording to {Path.GetFileName(_currentLogPath)} (interval: {_loggingIntervalMs}ms)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start recording: {ex.Message}";
        }
    }

    /// <summary>
    /// Stop recording and close CSV file.
    /// </summary>
    private void StopRecording()
    {
        try
        {
            _csvWriter?.Flush();
            _csvWriter?.Close();
            _csvWriter?.Dispose();
            _csvWriter = null;

            var savedPath = _currentLogPath;
            _currentLogPath = null;

            CurrentState = ViewerState.Live;
            StatusMessage = $"Recording stopped - Saved {_recordedSampleCount} samples to {Path.GetFileName(savedPath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error stopping recording: {ex.Message}";
            CurrentState = ViewerState.Live;
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
        OpenFluxCalculationCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Opens the Flux Calculation window with current chart data.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOpenFluxCalculation))]
    private void OpenFluxCalculation()
    {
        // Create a copy of current data
        var timeDataCopy = _timeData.ToList();
        var pressureDataCopy = _pressureData.ToList();

        var fluxWindow = new FluxCalculationWindow(timeDataCopy, pressureDataCopy);
        fluxWindow.Owner = System.Windows.Application.Current.MainWindow;
        fluxWindow.ShowDialog();
    }

    private bool CanOpenFluxCalculation() => _timeData.Count >= 2;

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

        while (series.TimeData.Count > MaxDataPoints)
        {
            series.TimeData.RemoveAt(0);
            series.PressureData.RemoveAt(0);
        }

        // Update selected series buffer for existing chart binding
        if (SelectedDevice?.DeviceId == deviceId)
        {
            UpdateSelectedSeriesData(deviceId);
            if (_timeData.Count == 2)
            {
                OpenFluxCalculationCommand.NotifyCanExecuteChanged();
            }
        }

        // Recording
        if (CurrentState == ViewerState.Recording)
        {
            var counter = _logTickCounters.TryGetValue(deviceId, out var value) ? value + 1 : 1;
            if (counter >= _logTickThreshold)
            {
                WriteToLog(reading);
                counter = 0;
            }

            _logTickCounters[deviceId] = counter;

            var recordingElapsed = DateTime.Now - _recordingStartTime;
            RecordingTime = recordingElapsed.ToString(@"hh\:mm\:ss");
        }

        DataUpdated?.Invoke();

        if (CurrentState == ViewerState.Live)
        {
            StatusMessage = $"Live - {_sampleCount} samples";
        }
    }

    private void WriteToLog(GaugeReading reading)
    {
        if (_csvWriter == null) return;

        try
        {
            var timestampIso = reading.Timestamp.ToString("O", CultureInfo.InvariantCulture);
            var pressureStr = reading.PressureTorr.ToString("G", CultureInfo.InvariantCulture);
            _csvWriter.WriteLine($"{timestampIso},{pressureStr}");

            _recordedSampleCount++;
            RecordedSampleCountDisplay = _recordedSampleCount;

            // Flush periodically
            if (_recordedSampleCount % 10 == 0)
            {
                _csvWriter.Flush();
            }

            StatusMessage = $"Recording - {_recordedSampleCount} samples @ {_loggingIntervalMs}ms";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Write error: {ex.Message}";
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

        _csvWriter?.Flush();
        _csvWriter?.Dispose();

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
