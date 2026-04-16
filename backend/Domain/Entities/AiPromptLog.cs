namespace backend.Domain.Entities;

public class AiPromptLog
{
    public long Id { get; set; }
    public int? JobId { get; set; }

    /// <summary>FK to MarketCluster when this log entry was produced by ClusterSynthesisService.</summary>
    public int? ClusterId { get; set; }

    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = "v1";

    public string PromptHash { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public string ResponseText { get; set; } = string.Empty;

    public bool CacheHit { get; set; }
    public bool IsSuccess { get; set; }
    public string Status { get; set; } = "unknown";
    public string? ErrorMessage { get; set; }

    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public int LatencyMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public JobOffer? Job { get; set; }
}
