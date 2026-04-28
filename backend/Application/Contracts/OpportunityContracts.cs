namespace backend.Application.Contracts;

// ── Query objects ──────────────────────────────────────────────────────────────

public class OpportunityQuery
{
    public string? LlmStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record OpportunityDto(
    int Id,
    int JobId,
    string Company,
    string JobTitle,
    string? JobDescription,
    string? TechStack,
    string? ProductIdeasJson,
    string LlmStatus,
    string Status,
    DateTime CreatedAt,
    DateTime? SynthesizedAt,
    // Bidirectional tracking: SourceIdeaIds of all products already created from this opportunity
    List<string> ConvertedIdeaIds);

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateProductFromOpportunityRequest(
    int OpportunityId,
    string Name,
    string ShortTechnicalDescription,
    string? SourceIdeaId = null);

// ── Idea Vault DTOs ───────────────────────────────────────────────────────────

public record OpportunityIdeaDto(
    string Id,
    string Name,
    string BusinessJustification,
    int? OpportunityId,
    string? OpportunityCompany,
    string? OpportunityJobTitle,
    DateTime CreatedAt);

public record UpdateOpportunityIdeaRequest(
    string Name,
    string BusinessJustification,
    int? OpportunityId);
