using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HVG2020B.Core;
using HVG2020B.Driver;
using ScottPlot;

namespace HVG2020B.Viewer.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly HVG2020BClient _client;
    private CancellationTokenSource? _cts;
    private readonly List<double> _timeData = new();
    private readonly List<double> _pressureData = new();
    private DateTime _startTime;
    private int _sampleCount;
    private bool _disposed;

    // Ring buffer for chart (last N seconds)
    private const int MaxDataPoints = 600; // 10 minutes at 1 Hz, or 1 minute at 10 Hz

    public MainViewModel()
    {
        _client = new HVG2020BClient();
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
    private bool _isConnected;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _currentPressure = "---";

    [ObservableProperty]
    private string _currentUnit = "Torr";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _sampleCountDisplay;

    [ObservableProperty]
    private string _elapsedTime = "00:00:00";

    #endregion

    #region Chart Data

    public List<double> TimeData => _timeData;
    public List<double> PressureData => _pressureData;

    public event Action? DataUpdated;

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

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
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

            IsConnected = true;
            IsRunning = true;
            _startTime = DateTime.Now;
            _sampleCount = 0;

            // Clear previous data
            _timeData.Clear();
            _pressureData.Clear();
            DataUpdated?.Invoke();

            StatusMessage = $"Connected to {SelectedPort}";

            _cts = new CancellationTokenSource();
            _ = RunAcquisitionLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            IsConnected = false;
            IsRunning = false;
        }
    }

    private bool CanStart() => !IsRunning && SelectedPort != null;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _cts?.Cancel();
        _client.Disconnect();

        IsRunning = false;
        IsConnected = false;
        StatusMessage = "Stopped";
    }

    private bool CanStop() => IsRunning;

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

        // Update elapsed time
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

        StatusMessage = $"Running - {_sampleCount} samples";
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _client.Dispose();
    }
}
