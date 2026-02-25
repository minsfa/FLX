namespace HVG2020B.Core.Models;

/// <summary>
/// Per-measurement state. One-way lifecycle: Ready -> Recording -> Done.
/// </summary>
public enum MeasurementState
{
    Ready,
    Recording,
    Done
}
