namespace backend.Domain.Entities;

public class Technology
{
    public int Id { get; set; }

    /// <summary>Canonical token name (e.g. "NET", "REACT", "OPENAI"). Unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable display name (e.g. ".NET", "React", "OpenAI").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Frontend | Backend | Cloud | Database | AI | DevOps | Architecture | Observability | Security | Messaging | Other</summary>
    public string Category { get; set; } = "Other";

    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    public int TotalMentions { get; set; }
    public int WeeklyMentions { get; set; }

    /// <summary>Percentage growth: (week0 - week1) / max(week1, 1) * 100</summary>
    public double GrowthRate { get; set; }

    /// <summary>0–100. Clamped GrowthRate: clamp(GrowthRate, -100, 100)</summary>
    public double MomentumScore { get; set; }

    /// <summary>0–100. log1p(mentions) normalized to log1p(200)</summary>
    public double DemandScore { get; set; }

    /// <summary>0–100. How many distinct clusters use this tech (saturates at 15).</summary>
    public double CompetitionScore { get; set; }

    /// <summary>0–100. Demand*0.4 + (100-Competition)*0.3 + AvgOpportunity*0.3</summary>
    public double OpportunityScore { get; set; }

    public double AvgLeadScore { get; set; }
    public double AvgUrgency { get; set; }

    /// <summary>How many distinct industries mention this tech.</summary>
    public int IndustryCoverageCount { get; set; }

    /// <summary>How many distinct clusters include this tech.</summary>
    public int ClusterCoverageCount { get; set; }

    /// <summary>0–100. Combined recency + positive momentum signal.</summary>
    public double EmergingScore { get; set; }

    public bool IsAiRelated { get; set; }
    public bool IsCloudRelated { get; set; }
    public bool IsLegacy { get; set; }

    /// <summary>Emerging | Growing | Mature | Declining | Legacy</summary>
    public string LifecycleStage { get; set; } = "Mature";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<TechnologyRelationship> SourceRelationships { get; set; } = [];
    public ICollection<TechnologyTrendSnapshot> Snapshots { get; set; } = [];
}
