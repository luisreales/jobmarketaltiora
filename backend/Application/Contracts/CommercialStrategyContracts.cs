namespace backend.Application.Contracts;

// ── Query ─────────────────────────────────────────────────────────────────────

public class CommercialStrategyQuery
{
    public string? Search { get; set; }
    public int? ProductId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record GenerateCommercialStrategyRequest(
    string Context,
    int? ProductId = null);

public record LinkStrategyToProductRequest(int? ProductId);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record CommercialStrategyDto(
    int Id,
    int? ProductId,
    string? ProductName_Linked,   // From Product nav property (live name)
    string ProductName,           // Denormalized (survives deletion)
    string CompanyContext,
    string RealBusinessProblem,
    string FinancialImpact,
    string MvpDefinition,
    string TargetBuyer,
    string PricingStrategy,
    string OutreachMessage,
    DateTime GeneratedAt);
