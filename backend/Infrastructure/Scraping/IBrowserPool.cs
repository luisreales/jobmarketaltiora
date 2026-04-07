using Microsoft.Playwright;

namespace backend.Infrastructure.Scraping;

public interface IBrowserPool
{
    Task<IBrowser> GetBrowserAsync(CancellationToken ct = default);
}
