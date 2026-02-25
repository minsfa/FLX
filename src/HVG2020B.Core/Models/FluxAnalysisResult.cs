namespace HVG2020B.Core.Models;

public class FluxAnalysisResult
{
    public int AnalysisId { get; set; }
    public DateTimeOffset CalculatedAt { get; set; }
    public string StudyId { get; set; } = "";
    public string StudyTitle { get; set; } = "";
    public string MeasurementId { get; set; } = "";
    public string DeviceId { get; set; } = "";

    /// <summary>
    /// The MeasurementRecord.MeasurementId this analysis belongs to (e.g., "M001").
    /// Null for legacy results.
    /// </summary>
    public string? MeasurementRecordId { get; set; }
    public string? ScreenshotPath { get; set; }

    // Input parameters snapshot
    public double MembraneArea { get; set; }
    public double Temperature { get; set; }
    public double FeedSidePressure { get; set; }
    public double ChamberVolume { get; set; }

    // Time range
    public double StartTime { get; set; }
    public double EndTime { get; set; }

    // Mode
    public CalculationMode Mode { get; set; } = CalculationMode.Permeation;

    // Results — Permeation
    public double Flux { get; set; }
    public double Permeance { get; set; }
    public double PermeanceGpu { get; set; }
    public double PressureChangeRate { get; set; }
    public double? RSquared { get; set; }
    public int DataPointCount { get; set; }

    // Results — Leak Rate
    public double LeakRateTorrLps { get; set; }
    public double LeakRatePaM3ps { get; set; }
    public double LeakRateMbarLps { get; set; }
    public string ConfigMemo { get; set; } = "";
}
