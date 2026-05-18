namespace backend.Domain.Entities;

public class AppSumoProduct
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public AppSumoCategory Category { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? OverallRating { get; set; }
    public int? TotalReviewCount { get; set; }
    public string? PricingModel { get; set; }
    public string? TagsJson { get; set; }

    public DateTime? ScrapedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<AppSumoReview> Reviews { get; set; } = [];
    public ProductScrapeState? ScrapeState { get; set; }
}
