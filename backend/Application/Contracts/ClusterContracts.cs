namespace backend.Application.Contracts;

// ── Query objects ──────────────────────────────────────────────────────────────

public class MarketClusterQuery
{
    public double? MinBlueOceanScore { get; set; }
    public string? PainCategory { get; set; }
    public string? Industry { get; set; }
    public string? OpportunityType { get; set; }
    public bool? IsActionable { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ClusterLeadsQuery
{
    public int? MinLeadScore { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record MarketClusterDto(
    int Id,
    string Label,
    string PainCategory,
    string Industry,
    string CompanyType,
    string NormalizedTechStack,

    // Market signals
    int JobCount,
    int DirectClientCount,
    double DirectClientRatio,
    double AvgOpportunityScore,
    double AvgUrgencyScore,
    double GrowthRate,

    // Scoring
    double BlueOceanScore,
    int RoiRank,

    // Decision Engine
    string OpportunityType,
    bool IsActionable,
    string RecommendedStrategy,
    int PriorityScore,

    // LLM synthesis
    string? SynthesizedPain,
    string? SynthesizedMvp,
    string? SynthesizedLeadMessage,
    string? MvpType,
    int? EstimatedBuildDays,
    decimal? EstimatedDealSizeUsd,
    string LlmStatus,

    DateTime LastUpdatedAt);

public record ClusterLeadDto(
    int JobId,
    string Company,
    string Title,
    string PainCategory,
    int OpportunityScore,
    int UrgencyScore,
    int LeadScore,
    string SuggestedSolution,
    string LeadMessage,
    bool IsDirectClient,
    string Source,
    string Url,
    DateTime CapturedAt);

public record ClusterRebuildResultDto(
    int ClustersUpserted,
    int ClustersEvaluated,
    int ActionableClusters,
    DateTime RanAt);

// ── Product Generator DTOs ────────────────────────────────────────────────────

public class ProductQuery
{
    public string? OpportunityType { get; set; }
    public string? Industry { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record ProductDto(
    int Id,
    string ProductName,
    string ProductDescription,
    string WhyNow,
    string Offer,
    string ActionToday,
    string TechFocus,
    int EstimatedBuildDays,
    decimal MinDealSizeUsd,
    decimal MaxDealSizeUsd,
    // Aggregated market signals
    int TotalJobCount,
    double AvgDirectClientRatio,
    double AvgUrgencyScore,
    double TopBlueOceanScore,
    int ClusterCount,
    // Decision
    int PriorityScore,
    string OpportunityType,
    string Industry,
    // LLM
    string? SynthesisDetailJson,
    string? TechnicalMvpJson,
    string LlmStatus,
    DateTime GeneratedAt,
    // Manual funnel
    int? OpportunityId,
    string? SourceIdeaId,
    string? ImageUrl,
    // Product lifecycle
    string Status);

public record UpdateProductRequest(
    string ProductName,
    string ProductDescription,
    string WhyNow,
    string Offer,
    string ActionToday,
    string TechFocus,
    int EstimatedBuildDays,
    decimal MinDealSizeUsd,
    decimal MaxDealSizeUsd,
    string OpportunityType,
    string Industry,
    string? ImageUrl,
    string Status);

public record ProductGenerateResultDto(
    int ProductsGenerated,
    int ActionableClusters,
    DateTime RanAt);
