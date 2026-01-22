namespace HVG2020B.Driver;

/// <summary>
/// Represents a pressure reading from the HVG-2020B gauge.
/// </summary>
public readonly record struct PressureReading
{
    /// <summary>
    /// Pressure value in Torr.
    /// </summary>
    public double PressureTorr { get; init; }

    /// <summary>
    /// Original unit from device response (e.g., "TORR", "mbar", "Pa"), if detected.
    /// Null if unit was not present in response.
    /// </summary>
    public string? UnitRaw { get; init; }

    /// <summary>
    /// Raw response line from the device (for debugging).
    /// </summary>
    public string RawLine { get; init; }

    /// <summary>
    /// True if unit conversion was applied (original was not Torr).
    /// </summary>
    public bool WasConverted { get; init; }
}
