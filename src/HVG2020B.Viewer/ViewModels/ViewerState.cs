namespace HVG2020B.Viewer.ViewModels;

/// <summary>
/// Viewer application states.
/// </summary>
public enum ViewerState
{
    /// <summary>
    /// Initial state - not connected to device.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Connected and showing live data on chart (not recording to file).
    /// </summary>
    Live,

    /// <summary>
    /// Connected and recording data to CSV file.
    /// </summary>
    Recording
}
