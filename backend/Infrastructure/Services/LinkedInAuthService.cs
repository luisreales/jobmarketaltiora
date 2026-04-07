using backend.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace backend.Infrastructure.Services;

public sealed class LinkedInAuthService(
    IOptions<LinkedInAuthOptions> options,
    ILogger<LinkedInAuthService> logger) : ILinkedInAuthService, IAsyncDisposable
{
    private static readonly SemaphoreSlim SessionFileLock = new(1, 1);
    private readonly SemaphoreSlim browserInitLock = new(1, 1);
    private IPlaywright? playwright;
    private IBrowser? sharedBrowser;

    public async Task LoginAndPersistSessionAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("LinkedIn username and password are required.");
        }

        var config = options.Value;
        EnsureSessionDirectory(config.SessionFilePath);

        logger.LogInformation("LinkedIn login started. LoginHeadless={LoginHeadless}", config.LoginHeadless);

        using var loginPlaywright = await Playwright.CreateAsync();
        await using var browser = await loginPlaywright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = config.LoginHeadless,
            SlowMo = Math.Clamp(config.SlowMoMs, 0, 5000),
            Args = DockerSafeBrowserArgs()
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = config.UserAgent
        });

        var page = await context.NewPageAsync();
        page.SetDefaultNavigationTimeout(Math.Clamp(config.NavigationTimeoutMs, 5000, 120000));

        try
        {
            await page.GotoAsync("https://www.linkedin.com/login", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            await FillFirstAsync(page, ["#username", "input[name='session_key']"], username.Trim());
            await FillFirstAsync(page, ["#password", "input[name='session_password']"], password);
            await page.ClickAsync("button[type='submit']");
            await page.WaitForTimeoutAsync(2000);

            if (IsCheckpointOrChallenge(page.Url))
            {
                logger.LogWarning("LinkedIn checkpoint/challenge detected after login submit.");

                if (config.LoginHeadless)
                {
                    throw new InvalidOperationException(
                        "LinkedIn requested checkpoint/challenge while LoginHeadless=true. Run manual login with LoginHeadless=false first.");
                }

                await WaitForManualChallengeCompletionAsync(page, context, config);
            }

            if (IsLoginPage(page.Url))
            {
                throw new InvalidOperationException("LinkedIn login failed or credentials were rejected.");
            }

            var hasSessionCookie = await HasLiAtCookieAsync(context);
            if (!hasSessionCookie)
            {
                throw new InvalidOperationException("LinkedIn session cookie was not created. Authentication is not valid.");
            }

            var storageState = await context.StorageStateAsync();
            await WriteSessionStateAsync(config.SessionFilePath, storageState);

            logger.LogInformation("LinkedIn session persisted at {SessionFilePath}", config.SessionFilePath);
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "LinkedIn login timed out.");
            throw;
        }
        catch (PlaywrightException ex)
        {
            logger.LogError(ex, "Playwright failed during LinkedIn login flow.");
            throw;
        }
    }

    public async Task<IBrowserContext> GetAuthenticatedContextAsync()
    {
        var config = options.Value;

        await EnsureSessionFileExistsAsync(config.SessionFilePath);
        await EnsureSharedBrowserAsync(config);

        var context = await sharedBrowser!.NewContextAsync(new BrowserNewContextOptions
        {
            StorageStatePath = config.SessionFilePath,
            UserAgent = config.UserAgent
        });

        var page = await context.NewPageAsync();
        page.SetDefaultNavigationTimeout(Math.Clamp(config.NavigationTimeoutMs, 5000, 120000));

        await page.GotoAsync("https://www.linkedin.com/feed", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        if (IsLoginPage(page.Url) || IsCheckpointOrChallenge(page.Url))
        {
            await context.CloseAsync();
            logger.LogWarning("LinkedIn session expired while creating authenticated context.");
            throw new InvalidOperationException("Session expired");
        }

        logger.LogInformation("LinkedIn authenticated browser context created.");
        return context;
    }

    public async Task<bool> IsSessionValidAsync()
    {
        var config = options.Value;

        return await ExecuteWithRetryAsync(async () =>
        {
            try
            {
                await EnsureSessionFileExistsAsync(config.SessionFilePath);
                await using var context = await GetAuthenticatedContextAsync();
                logger.LogInformation("LinkedIn session validation succeeded.");
                return true;
            }
            catch (FileNotFoundException)
            {
                logger.LogInformation("LinkedIn session file is missing.");
                return false;
            }
            catch (InvalidOperationException)
            {
                logger.LogInformation("LinkedIn session validation indicates expired/invalid session.");
                return false;
            }
            catch (PlaywrightException ex)
            {
                logger.LogWarning(ex, "Playwright error during LinkedIn session validation.");
                return false;
            }
        }, config.MaxRetryAttempts, config.RetryDelayMs);
    }

    public async ValueTask DisposeAsync()
    {
        if (sharedBrowser is not null)
        {
            await sharedBrowser.CloseAsync();
        }

        playwright?.Dispose();
        browserInitLock.Dispose();
    }

    private static string[] DockerSafeBrowserArgs()
    {
        return ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"];
    }

    private static bool IsLoginPage(string url)
    {
        return url.Contains("/login", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCheckpointOrChallenge(string url)
    {
        return url.Contains("/checkpoint", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("/challenge", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task FillFirstAsync(IPage page, IReadOnlyList<string> selectors, string value)
    {
        foreach (var selector in selectors)
        {
            if (await page.Locator(selector).CountAsync() > 0)
            {
                await page.FillAsync(selector, value);
                return;
            }
        }

        throw new InvalidOperationException($"Could not find expected input selector: {string.Join(", ", selectors)}");
    }

    private async Task WaitForManualChallengeCompletionAsync(IPage page, IBrowserContext context, LinkedInAuthOptions config)
    {
        logger.LogWarning("Complete verification manually, then press ENTER in the backend console to continue.");

        if (!Console.IsInputRedirected)
        {
            await Task.Run(Console.ReadLine);
        }

        var timeoutSeconds = Math.Clamp(config.ManualChallengeTimeoutSeconds, 30, 900);
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            await page.WaitForTimeoutAsync(1500);
            var url = page.Url;

            if (!IsCheckpointOrChallenge(url) && !IsLoginPage(url) && await HasLiAtCookieAsync(context))
            {
                logger.LogInformation("LinkedIn manual verification completed.");
                return;
            }
        }

        throw new TimeoutException("LinkedIn challenge was not completed before timeout.");
    }

    private static async Task<bool> HasLiAtCookieAsync(IBrowserContext context)
    {
        var cookies = await context.CookiesAsync(["https://www.linkedin.com"]);
        return cookies.Any(cookie =>
            cookie.Name.Equals("li_at", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(cookie.Value));
    }

    private static void EnsureSessionDirectory(string sessionFilePath)
    {
        var directory = Path.GetDirectoryName(sessionFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Session file path must include a directory.");
        }

        Directory.CreateDirectory(directory);
    }

    private async Task EnsureSessionFileExistsAsync(string sessionFilePath)
    {
        await SessionFileLock.WaitAsync();
        try
        {
            if (!File.Exists(sessionFilePath))
            {
                throw new FileNotFoundException("LinkedIn session file not found.", sessionFilePath);
            }
        }
        finally
        {
            SessionFileLock.Release();
        }
    }

    private async Task WriteSessionStateAsync(string sessionFilePath, string storageState)
    {
        await SessionFileLock.WaitAsync();
        try
        {
            await File.WriteAllTextAsync(sessionFilePath, storageState);
        }
        finally
        {
            SessionFileLock.Release();
        }
    }

    private async Task EnsureSharedBrowserAsync(LinkedInAuthOptions config)
    {
        if (sharedBrowser is not null)
        {
            return;
        }

        await browserInitLock.WaitAsync();
        try
        {
            if (sharedBrowser is not null)
            {
                return;
            }

            playwright ??= await Playwright.CreateAsync();
            sharedBrowser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = config.ReuseHeadless,
                Args = DockerSafeBrowserArgs()
            });

            logger.LogInformation("LinkedIn shared Chromium browser launched. ReuseHeadless={ReuseHeadless}", config.ReuseHeadless);
        }
        finally
        {
            browserInitLock.Release();
        }
    }

    private static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetryAttempts, int retryDelayMs)
    {
        var attempts = Math.Max(1, maxRetryAttempts);
        var delay = Math.Clamp(retryDelayMs, 100, 15000);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < attempts)
            {
                lastException = ex;
                await Task.Delay(delay);
            }
        }

        if (lastException is not null)
        {
            throw lastException;
        }

        return await operation();
    }
}