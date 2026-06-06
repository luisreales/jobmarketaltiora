namespace backend.Domain.Entities;

public class CompanyProfile
{
    public int Id { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Lowercase trimmed name — used as dedup key for upsert.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>DirectClient | Consulting | Mixed | Unknown</summary>
    public string CompanyType { get; set; } = "Unknown";

    public string PrimaryIndustry { get; set; } = "Unknown";

    /// <summary>JSON array of distinct canonical tech tokens (e.g. ["NET","AZURE","SQL"]).</summary>
    public string TechStackJson { get; set; } = "[]";

    public string TopPainCategory { get; set; } = string.Empty;

    public int TotalJobCount { get; set; }

    public double AvgUrgencyScore { get; set; }
    public double AvgOpportunityScore { get; set; }
    public double AvgLeadScore { get; set; }

    /// <summary>Jobs per 7-day window based on span between first and last seen.</summary>
    public double HiringVelocity { get; set; }

    /// <summary>True when the majority of their job posts are direct-client (not consulting).</summary>
    public bool IsDirectClient { get; set; }

    /// <summary>True when at least one job mentions AI/ML tech tokens.</summary>
    public bool HasAiInitiative { get; set; }

    /// <summary>True when the company is hiring for both legacy stack and cloud — migration signal.</summary>
    public bool HasCloudMigration { get; set; }

    /// <summary>
    /// Composite prospect score 0–100.
    /// = AvgLeadScore*0.4 + AvgUrgency*10*0.3 + (IsDirectClient?30:0)
    /// </summary>
    public double ProspectScore { get; set; }

    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
