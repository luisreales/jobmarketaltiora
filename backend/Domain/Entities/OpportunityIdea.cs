namespace backend.Domain.Entities;

/// <summary>
/// Persisted representation of an AI-generated product idea.
/// Outlives its parent Opportunity — OpportunityId becomes NULL if the
/// Opportunity is deleted (DeleteBehavior.SetNull), preserving LLM output.
/// </summary>
public class OpportunityIdea
{
    /// <summary>URL-friendly slug derived from the idea name (e.g. "cloud-audit-sprint").</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string BusinessJustification { get; set; } = string.Empty;

    /// <summary>Nullable FK — set to NULL when the parent Opportunity is deleted.</summary>
    public int? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
