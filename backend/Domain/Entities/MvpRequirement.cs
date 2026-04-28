namespace backend.Domain.Entities;

/// <summary>
/// Persisted AI-generated MVP Technical Requirements artifact.
/// Survives product deletion — ProductId becomes NULL (DeleteBehavior.SetNull).
/// Can be generated standalone via free-text context or linked to a Product.
/// </summary>
public class MvpRequirement
{
    public int Id { get; set; }

    /// <summary>Nullable FK — set to NULL when the linked product is deleted.</summary>
    public int? ProductId { get; set; }
    public ProductSuggestion? Product { get; set; }

    /// <summary>Denormalized product name — survives product deletion.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Free-text context used as LLM prompt input.</summary>
    public string CompanyContext { get; set; } = string.Empty;

    // ── LLM output fields ──────────────────────────────────────────────────────
    public string ArchitectureStrategy { get; set; } = string.Empty;
    public string RequiredTechStackJson { get; set; } = "[]";
    public string EstimatedTimelines { get; set; } = string.Empty;
    public string CoreFeaturesJson { get; set; } = "[]";

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
