using System.Collections.Concurrent;

namespace HVG2020B.Core;

/// <summary>
/// Manages multiple gauge devices and streams readings to consumers.
/// </summary>
public sealed class DeviceManager : IDisposable
{
    private readonly Dictionary<string, IGaugeDevice> _devices = new();
    private readonly Dictionary<string, CancellationTokenSource> _deviceTokens = new();
    private readonly Dictionary<string, Task> _deviceTasks = new();
    private readonly object _sync = new();
    private readonly TimeSpan _pollInterval;

    public DeviceManager(TimeSpan? pollInterval = null)
    {
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    }

    /// <summary>
    /// Queue of readings for UI or other consumers.
    /// </summary>
    public ConcurrentQueue<(string DeviceId, GaugeReading Reading)> ReadingQueue { get; } = new();

    /// <summary>
    /// Event raised when a new reading is available.
    /// </summary>
    public event EventHandler<(string DeviceId, GaugeReading Reading)>? ReadingReceived;

    /// <summary>
    /// Event raised when a device connection is lost.
    /// </summary>
    public event EventHandler<(string DeviceId, Exception Error)>? ConnectionLost;

    /// <summary>
    /// Adds a device and starts its async reading loop.
    /// </summary>
    public bool AddDevice(IGaugeDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        lock (_sync)
        {
            if (_devices.ContainsKey(device.DeviceId))
            {
                return false;
            }

            _devices[device.DeviceId] = device;
        }

        device.ConnectionLost += OnDeviceConnectionLost;

        var cts = new CancellationTokenSource();
        lock (_sync)
        {
            _deviceTokens[device.DeviceId] = cts;
            _deviceTasks[device.DeviceId] = Task.Run(() => PollDeviceAsync(device, cts.Token), cts.Token);
        }

        return true;
    }

    /// <summary>
    /// Removes a device and stops its async reading loop.
    /// </summary>
    public bool RemoveDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        IGaugeDevice? device;
        CancellationTokenSource? cts;
        Task? task;

        lock (_sync)
        {
            if (!_devices.TryGetValue(deviceId, out device))
            {
                return false;
            }

            _devices.Remove(deviceId);
            _deviceTokens.TryGetValue(deviceId, out cts);
            _deviceTokens.Remove(deviceId);
            _deviceTasks.TryGetValue(deviceId, out task);
            _deviceTasks.Remove(deviceId);
        }

        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        device.ConnectionLost -= OnDeviceConnectionLost;

        _ = task;

        return true;
    }

    private async Task PollDeviceAsync(IGaugeDevice device, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var reading = await device.ReadOnceAsync(token).ConfigureAwait(false);
                EnqueueReading(device.DeviceId, reading);
                await Task.Delay(_pollInterval, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation.
        }
        catch (Exception ex)
        {
            RaiseConnectionLost(device.DeviceId, ex);
        }
    }

    private void EnqueueReading(string deviceId, GaugeReading reading)
    {
        ReadingQueue.Enqueue((deviceId, reading));
        ReadingReceived?.Invoke(this, (deviceId, reading));
    }

    private void OnDeviceConnectionLost(object? sender, Exception error)
    {
        if (sender is not IGaugeDevice device)
        {
            return;
        }

        RaiseConnectionLost(device.DeviceId, error);
    }

    private void RaiseConnectionLost(string deviceId, Exception error)
    {
        ConnectionLost?.Invoke(this, (deviceId, error));

        if (_deviceTokens.TryGetValue(deviceId, out var cts))
        {
            cts.Cancel();
        }
    }

    public void Dispose()
    {
        List<IGaugeDevice> devices;

        lock (_sync)
        {
            devices = _devices.Values.ToList();
            _devices.Clear();
        }

        foreach (var device in devices)
        {
            device.ConnectionLost -= OnDeviceConnectionLost;
            device.Dispose();
        }

        foreach (var cts in _deviceTokens.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _deviceTokens.Clear();
        _deviceTasks.Clear();
    }
}
