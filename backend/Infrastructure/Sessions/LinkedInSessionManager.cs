using backend.Application.Interfaces;
using Microsoft.Playwright;

namespace backend.Infrastructure.Sessions;

public sealed class LinkedInSessionManager(
    IConfiguration configuration,
    IProviderSessionRepository sessionRepository,
    ILogger<LinkedInSessionManager> logger) : ISessionManager
{
    public async Task<bool> ValidateAsync(string provider, CancellationToken ct = default)
    {
        provider = provider.Trim().ToLowerInvariant();
        if (provider != "linkedin")
        {
            return true;
        }

        var storagePath = GetStorageStatePath(provider);
        if (!File.Exists(storagePath))
        {
            return false;
        }

        var navigationTimeoutMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:NavigationTimeoutMs") ?? 30000, 5000, 120000);
        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                StorageStatePath = storagePath,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
            });

            var page = await context.NewPageAsync();
            page.SetDefaultNavigationTimeout(navigationTimeoutMs);
            await page.GotoAsync("https://www.linkedin.com/feed", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForTimeoutAsync(1000);
            ct.ThrowIfCancellationRequested();

            var currentUrl = page.Url.ToLowerInvariant();
            if (currentUrl.Contains("/login") || currentUrl.Contains("/checkpoint") || currentUrl.Contains("/challenge"))
            {
                return false;
            }

            var cookies = await context.CookiesAsync(["https://www.linkedin.com"]);
            return cookies.Any(cookie =>
                cookie.Name.Equals("li_at", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(cookie.Value));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LinkedIn session validation failed.");
            return false;
        }
    }

    public async Task SaveAsync(string provider, object sessionData, CancellationToken ct = default)
    {
        provider = provider.Trim().ToLowerInvariant();
        var storagePath = GetStorageStatePath(provider);
        var directory = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = sessionData as string ?? sessionData.ToString() ?? string.Empty;
        await File.WriteAllTextAsync(storagePath, payload, ct);
    }

    public Task ClearAsync(string provider, CancellationToken ct = default)
    {
        provider = provider.Trim().ToLowerInvariant();
        var storagePath = GetStorageStatePath(provider);
        if (File.Exists(storagePath))
        {
            File.Delete(storagePath);
        }

        return Task.CompletedTask;
    }

    public async Task LoginAsync(string provider, string username, string password, CancellationToken ct = default)
    {
        provider = provider.Trim().ToLowerInvariant();
        if (provider != "linkedin")
        {
            return;
        }

        var headless = configuration.GetValue<bool?>("Jobs:Playwright:LoginHeadless") ?? false;
        var navigationTimeoutMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:NavigationTimeoutMs") ?? 30000, 5000, 120000);
        var manualChallengeTimeoutSeconds = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:ManualChallengeTimeoutSeconds") ?? 180, 30, 900);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
            SlowMo = 50,
            Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"]
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        });

        var page = await context.NewPageAsync();
        page.SetDefaultNavigationTimeout(navigationTimeoutMs);
        await page.GotoAsync("https://www.linkedin.com/login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        if (await page.Locator("#username").CountAsync() > 0)
        {
            await page.FillAsync("#username", username);
        }
        else
        {
            await page.FillAsync("input[name='session_key']", username);
        }

        if (await page.Locator("#password").CountAsync() > 0)
        {
            await page.FillAsync("#password", password);
        }
        else
        {
            await page.FillAsync("input[name='session_password']", password);
        }

        await page.ClickAsync("button[type='submit']");
        await page.WaitForTimeoutAsync(3000);
        ct.ThrowIfCancellationRequested();

        var currentUrl = page.Url.ToLowerInvariant();
        if (currentUrl.Contains("/checkpoint") || currentUrl.Contains("/challenge"))
        {
            if (headless)
            {
                throw new InvalidOperationException(
                    "LinkedIn requested checkpoint/challenge while LoginHeadless=true. Complete login in non-headless mode first.");
            }

            var deadline = DateTime.UtcNow.AddSeconds(manualChallengeTimeoutSeconds);
            var solved = false;
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                await page.WaitForTimeoutAsync(1500);
                currentUrl = page.Url.ToLowerInvariant();
                if (currentUrl.Contains("/feed") || currentUrl.Contains("/jobs") || currentUrl.Contains("/mynetwork"))
                {
                    var solvedState = await context.StorageStateAsync();
                    if (solvedState.Contains("\"li_at\"", StringComparison.OrdinalIgnoreCase))
                    {
                        solved = true;
                        break;
                    }
                }
            }

            if (!solved)
            {
                throw new InvalidOperationException("LinkedIn challenge was not completed before timeout.");
            }
        }

        if (currentUrl.Contains("/login") && await page.Locator("#password, input[name='session_password']").CountAsync() > 0)
        {
            throw new InvalidOperationException("LinkedIn login failed. Verify credentials.");
        }

        var storageState = await context.StorageStateAsync();
        if (!storageState.Contains("\"li_at\"", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("LinkedIn session cookie was not created. Login is not valid yet.");
        }

        await SaveAsync(provider, storageState, ct);

        var sessionHours = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:SessionHours") ?? 12, 1, 168);
        await sessionRepository.UpsertLoginAsync(provider, username.Trim(), DateTime.UtcNow.AddHours(sessionHours), ct);
    }

    public string GetStorageStatePath(string provider)
    {
        var configuredDirectory = configuration.GetValue<string>("Jobs:Playwright:SessionStateDirectory");
        var baseDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "playwright-state")
            : Path.GetFullPath(configuredDirectory);

        return Path.Combine(baseDirectory, $"{provider.Trim().ToLowerInvariant()}.json");
    }
}
