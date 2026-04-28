namespace backend.Domain.Entities;

/// <summary>
/// Represents a manual sales opportunity created by a human from a raw job posting.
/// This is the entry point of the human-in-the-loop B2B sales funnel.
/// </summary>
public class Opportunity
{
    public int Id { get; set; }

    public int JobId { get; set; }
    public JobOffer Job { get; set; } = null!;

    // Denormalized from JobOffer at creation time for funnel independence
    public string Company { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string? JobDescription { get; set; }
    public string? TechStack { get; set; }

    // LLM output from POST /api/opportunities/{id}/synthesize-ideas
    // JSON: [{"name": "...", "shortTechnicalDescription": "..."}]
    public string? ProductIdeasJson { get; set; }

    /// <summary>pending | completed | failed</summary>
    public string LlmStatus { get; set; } = "pending";

    /// <summary>active | converted — set to "converted" when a product is created from this opportunity</summary>
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SynthesizedAt { get; set; }

    // Navigation: products created from this opportunity
    public List<ProductSuggestion> Products { get; set; } = [];

    // Navigation: AI-generated ideas persisted in the Idea Vault
    public List<OpportunityIdea> Ideas { get; set; } = [];
}
