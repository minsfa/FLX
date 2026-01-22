using System.Collections.ObjectModel;
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
    private readonly HVG2020BClient _client;
    private CancellationTokenSource? _cts;
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

    // Blinking indicator timer
    private readonly DispatcherTimer _blinkTimer;

    // Ring buffer for chart (last N seconds)
    private const int MaxDataPoints = 600;

    public MainViewModel()
    {
        _client = new HVG2020BClient();

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
    private ObservableCollection<string> _availablePorts = new();

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    private ObservableCollection<string> _connectionModes = new() { "USB", "RS232" };

    [ObservableProperty]
    private string _selectedMode = "USB";

    [ObservableProperty]
    private int _baudRate = 19200;

    [ObservableProperty]
    private int _intervalMs = 500;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopRecordingCommand))]
    private ViewerState _currentState = ViewerState.Disconnected;

    [ObservableProperty]
    private string _currentPressure = "---";

    [ObservableProperty]
    private string _currentUnit = "Torr";

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

    #endregion

    #region Computed Properties

    public bool IsDisconnected => CurrentState == ViewerState.Disconnected;
    public bool IsLive => CurrentState == ViewerState.Live;
    public bool IsRecording => CurrentState == ViewerState.Recording;
    public bool IsConnected => CurrentState != ViewerState.Disconnected;

    #endregion

    #region Chart Data

    public List<double> TimeData => _timeData;
    public List<double> PressureData => _pressureData;

    public event Action? DataUpdated;

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

    #endregion

    #region Commands

    [RelayCommand]
    private void RefreshPorts()
    {
        var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
        AvailablePorts.Clear();
        foreach (var port in ports)
        {
            AvailablePorts.Add(port);
        }

        if (AvailablePorts.Count > 0 && SelectedPort == null)
        {
            SelectedPort = AvailablePorts[0];
        }

        StatusMessage = $"Found {ports.Length} COM port(s)";
    }

    /// <summary>
    /// Connect to device and enter Live mode.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrEmpty(SelectedPort))
        {
            StatusMessage = "Please select a COM port";
            return;
        }

        try
        {
            StatusMessage = $"Connecting to {SelectedPort}...";

            var settings = SelectedMode == "RS232"
                ? HVGSerialSettings.ForRs232(BaudRate)
                : HVGSerialSettings.ForUsb();

            await _client.ConnectAsync(SelectedPort, settings);

            // Transition to Live state
            CurrentState = ViewerState.Live;
            _startTime = DateTime.Now;
            _sampleCount = 0;

            // Clear previous data
            _timeData.Clear();
            _pressureData.Clear();
            DataUpdated?.Invoke();

            StatusMessage = $"Live - Connected to {SelectedPort}";

            // Start acquisition loop (chart only, no file logging)
            _cts = new CancellationTokenSource();
            _ = RunAcquisitionLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            CurrentState = ViewerState.Disconnected;
        }
    }

    private bool CanConnect() => CurrentState == ViewerState.Disconnected && SelectedPort != null;

    /// <summary>
    /// Disconnect from device.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private void Disconnect()
    {
        // If recording, stop first
        if (CurrentState == ViewerState.Recording)
        {
            StopRecording();
        }

        _cts?.Cancel();
        _client.Disconnect();

        CurrentState = ViewerState.Disconnected;
        CurrentPressure = "---";
        StatusMessage = "Disconnected";
    }

    private bool CanDisconnect() => CurrentState != ViewerState.Disconnected;

    /// <summary>
    /// Start recording to CSV file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartRecording))]
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

            CurrentState = ViewerState.Recording;
            StatusMessage = $"Recording to {Path.GetFileName(_currentLogPath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start recording: {ex.Message}";
        }
    }

    private bool CanStartRecording() => CurrentState == ViewerState.Live;

    /// <summary>
    /// Stop recording and close CSV file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopRecording))]
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

    private bool CanStopRecording() => CurrentState == ViewerState.Recording;

    [RelayCommand]
    private void ClearChart()
    {
        _timeData.Clear();
        _pressureData.Clear();
        _sampleCount = 0;
        SampleCountDisplay = 0;
        _startTime = DateTime.Now;
        ElapsedTime = "00:00:00";
        DataUpdated?.Invoke();
    }

    #endregion

    #region Acquisition Loop

    private async Task RunAcquisitionLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var reading = await _client.ReadOnceAsync(ct);

                // Update UI on dispatcher thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateReading(reading);
                });

                await Task.Delay(IntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Error: {ex.Message}";
                });

                // Brief delay before retry
                try
                {
                    await Task.Delay(1000, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void UpdateReading(GaugeReading reading)
    {
        _sampleCount++;
        SampleCountDisplay = _sampleCount;

        // Update current value display
        CurrentPressure = reading.PressureTorr.ToString("E3");
        CurrentUnit = reading.RawUnit ?? "Torr";

        // Update elapsed time (since connection)
        var elapsed = DateTime.Now - _startTime;
        ElapsedTime = elapsed.ToString(@"hh\:mm\:ss");

        // Add to chart data
        var timeSeconds = elapsed.TotalSeconds;
        _timeData.Add(timeSeconds);
        _pressureData.Add(reading.PressureTorr);

        // Trim to max points (ring buffer behavior)
        while (_timeData.Count > MaxDataPoints)
        {
            _timeData.RemoveAt(0);
            _pressureData.RemoveAt(0);
        }

        // Notify chart to update
        DataUpdated?.Invoke();

        // If recording, write to CSV
        if (CurrentState == ViewerState.Recording && _csvWriter != null)
        {
            try
            {
                var timestampIso = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                var pressureStr = reading.PressureTorr.ToString("G", CultureInfo.InvariantCulture);
                _csvWriter.WriteLine($"{timestampIso},{pressureStr}");

                _recordedSampleCount++;
                RecordedSampleCountDisplay = _recordedSampleCount;

                // Update recording time
                var recordingElapsed = DateTime.Now - _recordingStartTime;
                RecordingTime = recordingElapsed.ToString(@"hh\:mm\:ss");

                // Flush periodically
                if (_recordedSampleCount % 10 == 0)
                {
                    _csvWriter.Flush();
                }

                StatusMessage = $"Recording - {_recordedSampleCount} samples";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Write error: {ex.Message}";
            }
        }
        else if (CurrentState == ViewerState.Live)
        {
            StatusMessage = $"Live - {_sampleCount} samples";
        }
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _blinkTimer.Stop();
        _cts?.Cancel();
        _cts?.Dispose();

        _csvWriter?.Flush();
        _csvWriter?.Dispose();

        _client.Dispose();
    }
}
