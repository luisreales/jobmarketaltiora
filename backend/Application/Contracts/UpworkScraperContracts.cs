namespace backend.Application.Contracts;

public sealed record UpworkLoginResult(bool IsAuthenticated, DateTime? ExpiresAt, string? SessionToken);

public sealed record UpworkScrapeJobDto(
    string ExternalKey,
    string Title,
    string Company,
    string Location,
    string Description,
    string Url,
    string Source,
    string SearchTerm,
    DateTime CapturedAt,
    string? MetadataJson = null);

public sealed record UpworkScrapeResponse(bool IsAuthenticated, string? SessionToken, List<UpworkScrapeJobDto> Jobs);