using backend.Application.Contracts;
using backend.Infrastructure.Scraping;

namespace backend.Application.Interfaces;

public interface IUpworkScraperClient
{
    Task<UpworkLoginResult> LoginAsync(string username, string password, bool showBrowser = false, CancellationToken cancellationToken = default);
    Task<List<RawJobData>> ScrapeAsync(SearchRequest request, CancellationToken cancellationToken = default);
}