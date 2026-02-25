namespace HVG2020B.Core.Models;

/// <summary>
/// Represents a single recording session within a Study.
/// Each measurement has its own CSV file and analysis results.
/// </summary>
public class MeasurementRecord
{
    /// <summary>Auto-generated sequential ID: "M001", "M002", etc.</summary>
    public string MeasurementId { get; set; } = "";

    /// <summary>User-provided label (e.g., "Leak check", "Permeation run")</summary>
    public string Label { get; set; } = "";

    /// <summary>Devices used in this measurement</summary>
    public List<string> DeviceIds { get; set; } = new();

    /// <summary>CSV file name relative to study folder (e.g., "M001.csv")</summary>
    public string CsvFileName { get; set; } = "";

    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public int RecordedSampleCount { get; set; }

    /// <summary>Ready / Recording / Done</summary>
    public string State { get; set; } = "Ready";

    /// <summary>Analysis results for this measurement</summary>
    public List<FluxAnalysisResult> AnalysisResults { get; set; } = new();

    /// <summary>Counter for auto-incrementing AnalysisId within this measurement</summary>
    public int LatestAnalysisId { get; set; }
}
