using System.Text.Json.Serialization;

namespace HVG2020B.Core.Models;

public class StudyMetadata
{
    public string StudyId { get; set; } = "";

    public string Title { get; set; } = "";

    /// <summary>
    /// User-provided tag (displayed as "Id" in the UI).
    /// Serialized as "measurementId" for backward compat with old studies.json.
    /// </summary>
    [JsonPropertyName("measurementId")]
    public string UserTag { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public List<string> DeviceIds { get; set; } = new();

    public List<string> Tags { get; set; } = new();

    public string Notes { get; set; } = "";

    /// <summary>Study-level status: "Active" or "Closed"</summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// LEGACY: Kept for deserialization of old data.
    /// New studies use MeasurementRecord.LatestAnalysisId.
    /// </summary>
    public int LatestAnalysisId { get; set; }
}
