namespace backend.Application.Contracts;

// ── Query ─────────────────────────────────────────────────────────────────────

public class MvpRequirementQuery
{
    public string? Search { get; set; }
    public int? ProductId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record GenerateMvpRequirementRequest(
    string Context,
    int? ProductId = null);

public record LinkMvpToProductRequest(int? ProductId);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record MvpRequirementDto(
    int Id,
    int? ProductId,
    string? ProductName_Linked,   // From Product nav property (live name)
    string ProductName,           // Denormalized (survives deletion)
    string CompanyContext,
    string ArchitectureStrategy,
    string RequiredTechStackJson,
    string EstimatedTimelines,
    string CoreFeaturesJson,
    DateTime GeneratedAt);
