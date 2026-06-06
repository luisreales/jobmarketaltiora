namespace backend.Domain.Entities;

/// <summary>
/// Co-occurrence relationship between two technologies detected in the same job.
/// Composite PK: (SourceTechnologyId, TargetTechnologyId) — always stored with Source < Target by name.
/// </summary>
public class TechnologyRelationship
{
    public int SourceTechnologyId { get; set; }
    public int TargetTechnologyId { get; set; }

    public int CoOccurrenceCount { get; set; }

    /// <summary>CoOccurrenceCount / min(sourceMentions, targetMentions) * 100</summary>
    public double CorrelationScore { get; set; }

    /// <summary>Most common industry among jobs that contain both technologies.</summary>
    public string IndustryAffinity { get; set; } = "Unknown";

    /// <summary>Avg OpportunityScore of insights containing both technologies.</summary>
    public double OpportunityAffinity { get; set; }

    /// <summary>True when both technologies are AI-related.</summary>
    public bool AiAffinity { get; set; }

    public DateTime LastSeenAt { get; set; }

    public Technology SourceTechnology { get; set; } = null!;
    public Technology TargetTechnology { get; set; } = null!;
}
