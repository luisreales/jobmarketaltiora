namespace backend.Application.Contracts;

// ── Query / Request models ────────────────────────────────────────────────────

public class AppSumoReviewQuery
{
    public int? ProductId { get; set; }
    public int? CategoryId { get; set; }
    public byte? TacoRating { get; set; }       // 1, 2, or 3
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
}

public class AppSumoProductQuery
{
    public int? CategoryId { get; set; }
    public string? Search { get; set; }
    public string? ScrapeStatus { get; set; }   // Pending | Done | Failed | Skipped
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
}

public record StartScrapeRequest(
    string? StartCategorySlug = null,   // null = scrape all categories
    bool DryRun = false,
    int MaxProducts = 0);               // 0 = unlimited

// ── DTO models ────────────────────────────────────────────────────────────────

public record AppSumoCategoryDto(
    int Id,
    string Name,
    string Slug,
    string Url,
    string? ParentSlug,
    int ProductCount,
    DateTime? ScrapedAt);

public record AppSumoProductDto(
    int Id,
    int CategoryId,
    string CategoryName,
    string Name,
    string Slug,
    string Url,
    string? Description,
    decimal? OverallRating,
    int? TotalReviewCount,
    string? PricingModel,
    string? TagsJson,
    string ScrapeStatus,
    int LowRatingReviewCount,
    DateTime? ScrapedAt);

public record AppSumoReviewDto(
    long Id,
    int ProductId,
    string ProductName,
    string CategoryName,
    string? AppSumoReviewId,
    byte TacoRating,
    string? ReviewerName,
    DateOnly? ReviewDate,
    string ReviewText,
    int? FoundHelpful,
    bool IsVerified,
    DateTime CreatedAt);

public record AppSumoScrapeRunDto(
    int Id,
    DateTime StartedAt,
    DateTime? FinishedAt,
    string Status,
    int ProductsScraped,
    int ReviewsSaved,
    int ErrorCount,
    string? Notes);

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
