namespace backend.Domain.Entities;

/// <summary>
/// Persisted AI-generated commercial strategy artifact.
/// Survives product deletion — ProductId becomes NULL (DeleteBehavior.SetNull).
/// Can be generated standalone via free-text context or linked to a Product.
/// </summary>
public class CommercialStrategy
{
    public int Id { get; set; }

    /// <summary>Nullable FK — set to NULL when the linked product is deleted.</summary>
    public int? ProductId { get; set; }
    public ProductSuggestion? Product { get; set; }

    /// <summary>Denormalized product name — survives product deletion.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Free-text context (company/job info) used as LLM prompt input.</summary>
    public string CompanyContext { get; set; } = string.Empty;

    // ── LLM output fields ──────────────────────────────────────────────────────
    public string RealBusinessProblem { get; set; } = string.Empty;
    public string FinancialImpact { get; set; } = string.Empty;
    public string MvpDefinition { get; set; } = string.Empty;
    public string TargetBuyer { get; set; } = string.Empty;
    public string PricingStrategy { get; set; } = string.Empty;
    public string OutreachMessage { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
