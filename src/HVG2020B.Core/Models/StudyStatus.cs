namespace HVG2020B.Core.Models;

/// <summary>
/// Study-level status. Active = can add measurements, Closed = read-only.
/// Named StudyStatus to avoid collision with existing StudyState enum during transition.
/// </summary>
public enum StudyStatus
{
    Active,
    Closed
}
