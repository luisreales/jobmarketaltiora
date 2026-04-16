namespace backend.Application.Contracts;

public class MarketOpportunityQuery
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Source { get; set; }
    public int? MinOpportunityScore { get; set; }
    public int? MinUrgencyScore { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class MarketLeadsQuery
{
    public string? PainPoint { get; set; }
    public string? Source { get; set; }
    public int? MinScore { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class MarketTrendsQuery
{
    public int WindowDays { get; set; } = 14;
    public string? Source { get; set; }
}

public record MarketOpportunityDto(
    string PainPoint,
    string PainCategory,
    int OpportunityCount,
    double AvgOpportunityScore,
    double AvgUrgencyScore,
    string TopTechStack,
    string SuggestedMvp);

public record MarketLeadDto(
    int JobId,
    string Company,
    string Title,
    string PainPoint,
    int OpportunityScore,
    int UrgencyScore,
    string SuggestedSolution,
    string LeadMessage,
    string Source,
    string Url,
    DateTime CapturedAt);

public record MarketTrendDto(
    string PainCategory,
    int CurrentCount,
    int PreviousCount,
    double TrendPercentage);

public record JobInsightAnalysisResult(
    string MainPainPoint,
    string PainCategory,
    string PainDescription,
    string TechStack,
    int OpportunityScore,
    int UrgencyScore,
    string SuggestedSolution,
    string LeadMessage,
    bool IsDirectClient,
    string CompanyType,
    double ConfidenceScore,
    string DecisionSource,
    string Status,
    string? RawModelResponse,
    // Fase 0 — Data Quality Layer fields
    string Industry = "Unknown",
    string NormalizedTechStack = "Unknown",
    string TechTokensJson = "[]",
    int LeadScore = 0);
