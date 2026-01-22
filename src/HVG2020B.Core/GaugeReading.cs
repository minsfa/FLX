namespace HVG2020B.Core;

/// <summary>
/// Represents a single reading from a vacuum gauge.
/// </summary>
public readonly record struct GaugeReading
{
    /// <summary>
    /// Unique identifier of the device that produced this reading.
    /// </summary>
    public string DeviceId { get; init; }

    /// <summary>
    /// UTC timestamp when the reading was taken.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Pressure value in Torr.
    /// </summary>
    public double PressureTorr { get; init; }

    /// <summary>
    /// Original unit from device response (e.g., "TORR", "mbar"), if available.
    /// </summary>
    public string? RawUnit { get; init; }

    /// <summary>
    /// Raw response string from the device (for debugging).
    /// </summary>
    public string? RawResponse { get; init; }

    /// <summary>
    /// True if unit conversion was applied.
    /// </summary>
    public bool WasConverted { get; init; }

    /// <summary>
    /// Device status flags, if supported by the device.
    /// </summary>
    public GaugeStatus Status { get; init; }
}

/// <summary>
/// Gauge status flags.
/// </summary>
[Flags]
public enum GaugeStatus
{
    None = 0,
    Ok = 1,
    UnderRange = 2,
    OverRange = 4,
    SensorError = 8,
    CommunicationError = 16
}
