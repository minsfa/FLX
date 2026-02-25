namespace HVG2020B.Core.Models;

public class StudyRecord
{
    public StudyMetadata Metadata { get; set; } = new();

    /// <summary>List of measurements in this study</summary>
    public List<MeasurementRecord> Measurements { get; set; } = new();

    // --- Legacy fields (kept for backward compat deserialization) ---

    /// <summary>LEGACY: Single CSV path from old format. Null for new studies.</summary>
    public string? CsvFilePath { get; set; }

    /// <summary>LEGACY: Old study state string. New studies use Metadata.Status.</summary>
    public string StudyState { get; set; } = "Active";

    /// <summary>LEGACY: Old total sample count</summary>
    public int RecordedSampleCount { get; set; }

    /// <summary>LEGACY: Old study-level analysis results</summary>
    public List<FluxAnalysisResult> AnalysisResults { get; set; } = new();
}
