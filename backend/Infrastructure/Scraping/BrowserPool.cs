using Microsoft.Playwright;

namespace backend.Infrastructure.Scraping;

public sealed class BrowserPool(IConfiguration configuration, ILogger<BrowserPool> logger) : IBrowserPool, IAsyncDisposable
{
    private readonly SemaphoreSlim initLock = new(1, 1);
    private IPlaywright? playwright;
    private IBrowser? browser;

    public async Task<IBrowser> GetBrowserAsync(CancellationToken ct = default)
    {
        if (browser is not null)
        {
            return browser;
        }

        await initLock.WaitAsync(ct);
        try
        {
            if (browser is not null)
            {
                return browser;
            }

            var headless = configuration.GetValue<bool?>("Jobs:Playwright:Headless") ?? true;
            var slowMoMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:SlowMoMs") ?? 0, 0, 5000);

            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = headless,
                SlowMo = slowMoMs,
                Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"]
            });

            logger.LogInformation("Shared Playwright browser initialized.");
            return browser;
        }
        finally
        {
            initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
        {
            await browser.CloseAsync();
        }

        playwright?.Dispose();
        initLock.Dispose();
    }
}