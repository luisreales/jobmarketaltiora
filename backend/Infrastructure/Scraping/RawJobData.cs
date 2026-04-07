namespace backend.Infrastructure.Scraping;

public sealed record RawJobData(
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
