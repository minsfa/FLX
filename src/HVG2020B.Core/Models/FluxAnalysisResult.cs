namespace HVG2020B.Core.Models;

public class FluxAnalysisResult
{
    public int AnalysisId { get; set; }
    public DateTimeOffset CalculatedAt { get; set; }
    public string StudyId { get; set; } = "";
    public string StudyTitle { get; set; } = "";
    public string MeasurementId { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string? ScreenshotPath { get; set; }

    // Input parameters snapshot
    public double MembraneArea { get; set; }
    public double Temperature { get; set; }
    public double FeedSidePressure { get; set; }
    public double ChamberVolume { get; set; }

    // Time range
    public double StartTime { get; set; }
    public double EndTime { get; set; }

    // Results
    public double Flux { get; set; }
    public double Permeance { get; set; }
    public double PermeanceGpu { get; set; }
    public double PressureChangeRate { get; set; }
    public double? RSquared { get; set; }
    public int DataPointCount { get; set; }
}
