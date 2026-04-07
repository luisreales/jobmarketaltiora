using backend.Application.Contracts;

namespace backend.Infrastructure.Scraping;

public interface IPlaywrightScraper
{
    Task<List<RawJobData>> ScrapeLinkedInAsync(SearchRequest request, string storageStatePath, CancellationToken ct);
    Task<List<RawJobData>> ScrapeGoogleAsync(string provider, SearchRequest request, CancellationToken ct);
}
