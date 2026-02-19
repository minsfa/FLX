namespace HVG2020B.Core;

/// <summary>
/// Emulator device that generates sine-wave pressure data without physical hardware.
/// </summary>
public sealed class EmulatorDevice : IGaugeDevice
{
    private const double BasePressureTorr = 1.0E-3;
    private const double Amplitude = 0.5E-3;
    private const double PeriodSeconds = 60.0;

    private static int _instanceCount;

    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private readonly double _phaseOffset;
    private bool _connected;
    private bool _disposed;

    public EmulatorDevice()
    {
        var id = Interlocked.Increment(ref _instanceCount);
        DeviceId = $"EMU-{id:D2}";
        DisplayName = $"Emulator #{id}";
        // Each instance shifts by pi so adjacent emulators move in opposite directions
        _phaseOffset = (id - 1) * Math.PI;
    }

    public string DeviceId { get; }
    public string DeviceType => "Emulator";
    public string DisplayName { get; set; }
    public bool IsConnected => _connected;
    public string? PortName => _connected ? "EMULATOR" : null;

    public event EventHandler<GaugeReading>? ReadingReceived;
#pragma warning disable CS0067 // Emulator never loses connection
    public event EventHandler<Exception>? ConnectionLost;
#pragma warning restore CS0067

    public Task ConnectAsync(string portName, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        _connected = true;
        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        _connected = false;
    }

    public Task<GaugeReading> ReadOnceAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_connected)
            throw new InvalidOperationException("Emulator not connected");

        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        var elapsed = (now - _startTime).TotalSeconds;
        var pressure = BasePressureTorr + Amplitude * Math.Sin(2.0 * Math.PI * elapsed / PeriodSeconds + _phaseOffset);

        var reading = new GaugeReading
        {
            DeviceId = DeviceId,
            Timestamp = now,
            PressureTorr = pressure,
            RawUnit = "TORR",
            RawResponse = $"{pressure:E4} TORR>",
            WasConverted = false,
            Status = GaugeStatus.Ok
        };

        ReadingReceived?.Invoke(this, reading);
        return Task.FromResult(reading);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}
