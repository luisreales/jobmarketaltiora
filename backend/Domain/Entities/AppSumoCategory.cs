namespace backend.Domain.Entities;

public class AppSumoCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ParentSlug { get; set; }
    public DateTime? ScrapedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<AppSumoProduct> Products { get; set; } = [];
}
