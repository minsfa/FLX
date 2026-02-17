namespace HVG2020B.Core.Models;

public class StudyRecord
{
    public StudyMetadata Metadata { get; set; } = new();
    public string? CsvFilePath { get; set; }
    public string StudyState { get; set; } = "Ready";
    public int RecordedSampleCount { get; set; }
    public List<FluxAnalysisResult> AnalysisResults { get; set; } = new();
}
