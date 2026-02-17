namespace HVG2020B.Driver;

/// <summary>
/// Abstraction for gauge client, enabling emulator mode.
/// </summary>
public interface IGaugeClient : IDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(string portName, HVGSerialSettings settings, CancellationToken cancellationToken = default);

    Task<PressureReading> ReadPressureOnceAsync(CancellationToken cancellationToken = default);

    void Disconnect();
}
