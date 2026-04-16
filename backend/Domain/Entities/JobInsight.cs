namespace backend.Domain.Entities;

public class JobInsight
{
    public int Id { get; set; }
    public int JobId { get; set; }

    public string MainPainPoint { get; set; } = string.Empty;
    public string PainCategory { get; set; } = string.Empty;
    public string PainDescription { get; set; } = string.Empty;
    public string TechStack { get; set; } = string.Empty;

    public bool IsDirectClient { get; set; }
    public string CompanyType { get; set; } = string.Empty;

    public int OpportunityScore { get; set; }
    public int UrgencyScore { get; set; }

    public string SuggestedSolution { get; set; } = string.Empty;
    public string LeadMessage { get; set; } = string.Empty;

    public double ConfidenceScore { get; set; }
    public string DecisionSource { get; set; } = "Rules";
    public string Status { get; set; } = "Processed";

    public bool IsProcessed { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string EngineVersion { get; set; } = "rules-v1";
    public string? RawModelResponse { get; set; }

    // Fase 0 — Data Quality Layer
    /// <summary>Industry inferred by IndustryClassifier (e.g. "Fintech", "Health").</summary>
    public string Industry { get; set; } = "Unknown";

    /// <summary>Canonical tech tokens joined by ", " (e.g. "NET, SQL, DOCKER").</summary>
    public string NormalizedTechStack { get; set; } = "Unknown";

    /// <summary>JSON array of individual canonical tech tokens for exact matching in queries.</summary>
    public string TechTokensJson { get; set; } = "[]";

    /// <summary>
    /// Composite lead score 0–100 (OpportunityScore + Urgency + DirectClient + Recency).
    /// Used to rank leads within a cluster.
    /// </summary>
    public int LeadScore { get; set; }

    /// <summary>FK to MarketCluster once the Cluster Engine assigns this insight to a cluster.</summary>
    public int? ClusterId { get; set; }

    public JobOffer? Job { get; set; }
}
