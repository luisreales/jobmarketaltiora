namespace backend.Domain.Entities;

/// <summary>
/// Weekly time-series snapshot of technology mention volume.
/// Append-only — existing rows are never overwritten.
/// </summary>
public class TechnologyTrendSnapshot
{
    public int Id { get; set; }
    public int TechnologyId { get; set; }

    /// <summary>UTC Monday of the week this snapshot represents.</summary>
    public DateTime SnapshotWeek { get; set; }

    public int MentionCount { get; set; }
    public int UniqueJobCount { get; set; }
    public double AvgOpportunityScore { get; set; }

    public DateTime CreatedAt { get; set; }

    public Technology Technology { get; set; } = null!;
}
