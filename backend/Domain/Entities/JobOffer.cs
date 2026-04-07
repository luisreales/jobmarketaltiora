namespace backend.Domain.Entities;

public class JobOffer
{
    public int Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? SalaryRange { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? Seniority { get; set; }
    public string? ContractType { get; set; }
    public string Source { get; set; } = "unknown";
    public string SearchTerm { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public string? MetadataJson { get; set; }
    public string Category { get; set; } = "Unknown";
    public int OpportunityScore { get; set; }
    public bool IsConsultingCompany { get; set; }
    public string CompanyType { get; set; } = "Unknown";
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
