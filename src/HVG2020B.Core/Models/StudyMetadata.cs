namespace HVG2020B.Core.Models;

public class StudyMetadata
{
    public string StudyId { get; set; } = "";

    public string Title { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public List<string> DeviceIds { get; set; } = new();

    public List<string> Tags { get; set; } = new();

    public string Notes { get; set; } = "";

    public int LatestAnalysisId { get; set; }
}
