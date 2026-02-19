namespace HVG2020B.Core;

/// <summary>
/// Common interface for all vacuum gauge devices.
/// Implement this interface to add support for new gauge types.
/// </summary>
public interface IGaugeDevice : IDisposable
{
    /// <summary>
    /// Unique identifier for this device instance.
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// Device type/model name (e.g., "HVG-2020B", "MKS-925").
    /// </summary>
    string DeviceType { get; }

    /// <summary>
    /// Display name for this device (user-configurable).
    /// </summary>
    string DisplayName { get; set; }

    /// <summary>
    /// Whether the device is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Port name the device is connected to (e.g., "COM3").
    /// </summary>
    string? PortName { get; }

    /// <summary>
    /// Connects to the device.
    /// </summary>
    /// <param name="portName">COM port name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ConnectAsync(string portName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the device.
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Reads a single pressure value from the device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Gauge reading with pressure and metadata</returns>
    Task<GaugeReading> ReadOnceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when a new reading is available (for streaming mode).
    /// </summary>
    event EventHandler<GaugeReading>? ReadingReceived;

    /// <summary>
    /// Event raised when the connection is lost.
    /// </summary>
    event EventHandler<Exception>? ConnectionLost;
}
