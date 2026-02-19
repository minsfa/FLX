namespace HVG2020B.Driver;

/// <summary>
/// Emulator client that generates sine-wave pressure data without a physical device.
/// </summary>
public sealed class EmulatorClient : IGaugeClient
{
    private const double BasePressureTorr = 1.0E-3;
    private const double Amplitude = 0.5E-3;       // +-50% of base
    private const double PeriodSeconds = 60.0;

    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private bool _connected;
    private bool _disposed;

    public bool IsConnected => _connected;

    public Task ConnectAsync(string portName, HVGSerialSettings settings, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        _connected = true;
        Console.WriteLine("[EMULATOR] Connected (simulated)");
        return Task.CompletedTask;
    }

    public Task<PressureReading> ReadPressureOnceAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_connected)
            throw new HVGProtocolException("Not connected to device");

        cancellationToken.ThrowIfCancellationRequested();

        var elapsed = (DateTimeOffset.UtcNow - _startTime).TotalSeconds;
        var pressure = BasePressureTorr + Amplitude * Math.Sin(2.0 * Math.PI * elapsed / PeriodSeconds);

        var reading = new PressureReading
        {
            PressureTorr = pressure,
            UnitRaw = "TORR",
            RawLine = $"{pressure:E4} TORR>",
            WasConverted = false
        };

        return Task.FromResult(reading);
    }

    public void Disconnect()
    {
        _connected = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}
