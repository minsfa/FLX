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

        RefreshPorts();
    }

    #region Observable Properties

    [ObservableProperty]
    private ObservableCollection<DeviceItem> _devices = new();

    [ObservableProperty]
    private DeviceItem? _selectedDevice;

    [ObservableProperty]
    private ObservableCollection<string> _availablePorts = new();

    public static IReadOnlyList<string> ConnectionModes { get; } = new[] { "USB", "RS232" };

    [ObservableProperty]
    private ViewerState _currentState = ViewerState.Disconnected;

    [ObservableProperty]
    private string _statusMessage = "Ready";

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
    private void AddDevice()
    {
        var deviceId = $"HVG-{Devices.Count + 1:00}";
        var device = new HVG2020BClient(deviceId);
        var item = new DeviceItem(device);

        Devices.Add(item);
        item.PropertyChanged += OnDeviceItemPropertyChanged;
        if (item.SelectedPort == null && AvailablePorts.Count > 0)
        {
            item.SelectedPort = AvailablePorts[0];
        }
        EnsureDeviceSeries(deviceId);

        if (SelectedDevice == null)
        {
            SelectedDevice = item;
        }

        StatusMessage = $"Device added: {deviceId}";
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
        AvailablePorts.Clear();
        foreach (var port in ports)
        {
            AvailablePorts.Add(port);
        }

        foreach (var device in Devices)
        {
            if (device.SelectedPort == null || !AvailablePorts.Contains(device.SelectedPort))
            {
                device.SelectedPort = AvailablePorts.FirstOrDefault();
            }
        }

        StatusMessage = $"Found {ports.Length} COM port(s)";
    }

    [RelayCommand]
    private async Task ConnectDeviceAsync(DeviceItem deviceItem)
    {
        if (deviceItem == null)
        {
            StatusMessage = "Please select a device";
            return;
        }

        try
        {
            deviceItem.IsConnecting = true;

            if (string.IsNullOrEmpty(deviceItem.SelectedPort))
            {
                StatusMessage = "Please select a COM port";
                return;
            }

            if (deviceItem.IsConnected)
            {
                StatusMessage = $"{deviceItem.DeviceId} is already connected";
                return;
            }

            if (Devices.Any(d =>
                d.DeviceId != deviceItem.DeviceId &&
                d.IsConnected &&
                d.SelectedPort == deviceItem.SelectedPort))
            {
                StatusMessage = $"Port {deviceItem.SelectedPort} is already in use";
                return;
            }

            string connectionInfo;
            var device = deviceItem.Device;

            if (deviceItem.SelectedMode == "RS232")
            {
                if (device is HVG2020BClient hvgClient)
                {
                    // RS232: auto-scan baud rates
                    StatusMessage = $"Scanning baud rates on {deviceItem.SelectedPort}...";
                    var detectedBaudRate = await hvgClient.ConnectWithAutoScanAsync(deviceItem.SelectedPort);
                    deviceItem.BaudRate = detectedBaudRate;
                    connectionInfo = $"{deviceItem.SelectedPort} @ {detectedBaudRate} bps (auto-detected)";
                }
                else
                {
                    StatusMessage = $"Connecting to {deviceItem.SelectedPort}...";
                    await device.ConnectAsync(deviceItem.SelectedPort);
                    connectionInfo = $"{deviceItem.SelectedPort} (RS232)";
                }
            }
            else
            {
                StatusMessage = $"Connecting to {deviceItem.SelectedPort}...";
                if (device is HVG2020BClient hvgClient)
                {
                    var settings = HVGSerialSettings.ForUsb();
                    await hvgClient.ConnectAsync(deviceItem.SelectedPort, settings);
                }
                else
                {
                    await device.ConnectAsync(deviceItem.SelectedPort);
                }
                connectionInfo = $"{deviceItem.SelectedPort} (USB)";
            }

            deviceItem.IsConnected = device.IsConnected;
            deviceItem.PortName = device.PortName;

            if (_managedDeviceIds.Add(device.DeviceId))
            {
                _deviceManager.AddDevice(device);
            }

            if (CurrentState == ViewerState.Disconnected)
            {
                CurrentState = ViewerState.Live;
                _startTime = DateTime.Now;
                _sampleCount = 0;
                _recordedSampleCount = 0;
            }

            // Calculate initial tick threshold
            _logTickThreshold = _loggingIntervalMs / LiveIntervalMs;

            StatusMessage = $"Live - Connected to {connectionInfo}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            UpdateCurrentStateFromDevices();
        }
        finally
        {
            deviceItem.IsConnecting = false;
        }
    }

    /// <summary>
    /// Disconnect from device.
    /// </summary>
    [RelayCommand]
    private void DisconnectDevice(DeviceItem deviceItem)
    {
        if (deviceItem == null)
        {
            StatusMessage = "No device selected";
            return;
        }

        // If recording, stop first
        if (CurrentState == ViewerState.Recording)
        {
            StopRecording();
        }

        deviceItem.Device.Disconnect();
        deviceItem.IsConnected = false;
        deviceItem.PortName = null;
        deviceItem.CurrentPressure = "---";

        _deviceManager.RemoveDevice(deviceItem.DeviceId);
        _managedDeviceIds.Remove(deviceItem.DeviceId);

        UpdateCurrentStateFromDevices();
        StatusMessage = $"Disconnected {deviceItem.DeviceId}";
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _blinkTimer.Stop();

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
    private bool _isSettingsExpanded;

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    private string _selectedMode = "USB";

    [ObservableProperty]
    private int _baudRate = 19200;

    [ObservableProperty]
    private bool _isConnecting;

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
