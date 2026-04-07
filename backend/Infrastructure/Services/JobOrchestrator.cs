using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using Microsoft.Playwright;

namespace backend.Infrastructure.Services;

public class JobOrchestrator(
    IJobRepository jobRepository,
    IProviderSessionRepository sessionRepository,
    IConfiguration configuration,
    ILogger<JobOrchestrator> logger) : IJobOrchestrator
{
    public async Task LoginAsync(string provider, string username, string password, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProvider(provider);
        if (!ProviderRequiresAuthentication(normalized))
        {
            return;
        }

        // Credentials are always loaded from configuration for provider login.
        username = configuration.GetValue<string>("Jobs:Credentials:LinkedIn:Username") ?? string.Empty;
        password = configuration.GetValue<string>("Jobs:Credentials:LinkedIn:Password") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Missing Jobs:Credentials:LinkedIn:Username or Password in appsettings");
        }

        if (normalized == "linkedin")
        {
            await LoginLinkedInAndPersistSessionAsync(username.Trim(), password, cancellationToken);
        }

        var sessionHours = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:SessionHours") ?? 12, 1, 168);
        await sessionRepository.UpsertLoginAsync(normalized, username.Trim(), DateTime.UtcNow.AddHours(sessionHours), cancellationToken);
    }

    public async Task LogoutAsync(string provider, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProvider(provider);

        await sessionRepository.ClearSessionAsync(normalized, cancellationToken);

        if (normalized == "linkedin")
        {
            var storagePath = GetStorageStatePath("linkedin");
            if (File.Exists(storagePath))
            {
                File.Delete(storagePath);
            }
        }
    }

    public async Task<(bool isAuthenticated, DateTime? lastLoginAt, DateTime? lastUsedAt, DateTime? expiresAt)> GetAuthStatusAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProvider(provider);
        if (!ProviderRequiresAuthentication(normalized))
        {
            return (true, null, null, null);
        }

        var session = await sessionRepository.GetCurrentAsync(normalized, cancellationToken);
        var isAuthenticated = await IsProviderAuthenticatedAsync(normalized, cancellationToken);
        return (isAuthenticated, session?.LastLoginAt, session?.LastUsedAt, session?.ExpiresAt);
    }

    public async Task<(int savedCount, int totalFound)> SearchAndSaveAsync(
        string query,
        string? location,
        int limit,
        IReadOnlyCollection<string>? providersFilter = null,
        int? totalPaging = null,
        int? startPage = null,
        int? endPage = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("query is required");
        }

        var selectedProviders = ResolveSearchProviders(providersFilter);
        var allResults = new List<JobOffer>();

        foreach (var provider in selectedProviders)
        {
            if (ProviderRequiresAuthentication(provider) && !await IsProviderAuthenticatedAsync(provider, cancellationToken))
            {
                logger.LogWarning("Skipping provider {Provider} because auth session is not valid.", provider);
                continue;
            }

            if (provider == "linkedin")
            {
                var isSessionValid = await ValidateLinkedInSessionForScrapingAsync(cancellationToken);
                if (!isSessionValid)
                {
                    await sessionRepository.ClearSessionAsync(provider, cancellationToken);
                    throw new InvalidOperationException(
                        "LinkedIn session is expired or invalid. Re-authenticate with POST /api/auth/login and complete manual verification if prompted.");
                }
            }

            try
            {
                var providerResults = await SearchByProviderAsync(provider, query.Trim(), location, limit, totalPaging, startPage, endPage, cancellationToken);
                allResults.AddRange(providerResults);

                if (ProviderRequiresAuthentication(provider))
                {
                    await sessionRepository.MarkUsedAsync(provider, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Provider {Provider} failed while scraping query {Query}", provider, query);
            }
        }

        var savedCount = await jobRepository.UpsertRangeAsync(allResults, cancellationToken);
        return (savedCount, allResults.Count);
    }

    public Task<List<JobOffer>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        return jobRepository.GetAllAsync(cancellationToken);
    }

    public Task<JobOffer?> GetJobByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return jobRepository.GetByIdAsync(id, cancellationToken);
    }

    private List<string> ResolveSearchProviders(IReadOnlyCollection<string>? providersFilter)
    {
        if (providersFilter is { Count: > 0 })
        {
            return providersFilter
                .Select(NormalizeProvider)
                .Distinct()
                .ToList();
        }

        var enabledProviders = configuration.GetSection("Jobs:Providers:Enabled").Get<List<string>>() ?? [];
        return enabledProviders.Count == 0
            ? ["linkedin", "indeed"]
            : enabledProviders.Select(NormalizeProvider).Distinct().ToList();
    }

    private static string NormalizeProvider(string provider)
    {
        return provider.Trim().ToLowerInvariant();
    }

    private static bool ProviderRequiresAuthentication(string provider)
    {
        return provider == "linkedin";
    }

    private Task<List<JobOffer>> SearchByProviderAsync(
        string provider,
        string query,
        string? location,
        int limit,
        int? totalPaging,
        int? startPage,
        int? endPage,
        CancellationToken cancellationToken)
    {
        return provider switch
        {
            "linkedin" => SearchLinkedInDirectWithPlaywrightAsync(query, location, limit, totalPaging, startPage, endPage, cancellationToken),
            "indeed" => SearchByGoogleWithPlaywrightAsync("indeed", query, location, limit, cancellationToken),
            _ => throw new InvalidOperationException($"provider '{provider}' is not supported")
        };
    }

    private async Task<List<JobOffer>> SearchLinkedInDirectWithPlaywrightAsync(
        string query,
        string? location,
        int limit,
        int? totalPaging,
        int? startPage,
        int? endPage,
        CancellationToken cancellationToken)
    {
        var normalizedLocation = string.IsNullOrWhiteSpace(location) ? "Remote" : location.Trim();

        var headless = configuration.GetValue<bool?>("Jobs:Playwright:Headless") ?? true;
        var slowMoMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:SlowMoMs") ?? 0, 0, 5000);
        var navigationTimeoutMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:NavigationTimeoutMs") ?? 30000, 5000, 120000);

        var resultLoadTimeoutMs = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:ResultLoadTimeoutMs") ?? 20000, 5000, 120000);
        var detailLoadTimeoutMs = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:DetailLoadTimeoutMs") ?? 12000, 3000, 60000);
        var scrollPauseMs = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:ScrollPauseMs") ?? 1200, 200, 10000);
        var clickPauseMs = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:ClickPauseMs") ?? 600, 100, 5000);
        var maxScrollAttemptsPerPage = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:MaxScrollAttemptsPerPage") ?? 8, 1, 50);
        var maxPages = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:MaxPages") ?? 5, 1, 50);
        var estimatedJobsPerPage = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:EstimatedJobsPerPage") ?? 25, 1, 50);

        var requestedStartPage = Math.Clamp(startPage ?? 1, 1, maxPages);
        var requestedPaging = Math.Clamp(totalPaging ?? 1, 1, maxPages);
        var requestedEndPage = endPage.HasValue
            ? Math.Clamp(endPage.Value, requestedStartPage, maxPages)
            : Math.Min(maxPages, requestedStartPage + requestedPaging - 1);
        var pagesToProcess = Math.Max(1, requestedEndPage - requestedStartPage + 1);
        var pagingExpectedJobs = pagesToProcess * estimatedJobsPerPage;
        var targetCount = Math.Clamp(Math.Max(limit, pagingExpectedJobs), 1, 5000);

        logger.LogInformation(
            "LinkedIn paging request resolved to range {StartPage}..{EndPage} (requested totalPaging={TotalPaging}, limit={Limit}).",
            requestedStartPage,
            requestedEndPage,
            totalPaging,
            limit);

        var now = DateTime.UtcNow;
        var offersByUrl = new Dictionary<string, JobOffer>(StringComparer.OrdinalIgnoreCase);
        var seenJobIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
            SlowMo = slowMoMs,
            Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"]
        });

        var storagePath = GetStorageStatePath("linkedin");
        if (!File.Exists(storagePath))
        {
            throw new InvalidOperationException("LinkedIn session not found. Login first using /api/auth/login");
        }

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            StorageStatePath = storagePath,
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        });

        var page = await context.NewPageAsync();
        page.SetDefaultNavigationTimeout(navigationTimeoutMs);

        await page.GotoAsync(BuildLinkedInJobsSearchUrl(query, normalizedLocation), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        if (IsLinkedInAuthRedirect(page.Url))
        {
            throw new InvalidOperationException("LinkedIn session expired. Re-authenticate with POST /api/auth/login.");
        }

        logger.LogInformation("LinkedIn direct scraping step 1/4: applying search input.");
        await ApplyLinkedInSearchInputAsync(page, query, clickPauseMs);
        logger.LogInformation("LinkedIn direct scraping step 2/4: waiting jobs search page ready.");
        await WaitForLinkedInSearchPageAsync(page, resultLoadTimeoutMs);

        var currentPage = 1;
        while (currentPage < requestedStartPage)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForLinkedInResultsReadyAsync(page, resultLoadTimeoutMs);
            var moved = await GoToNextLinkedInPageAsync(page, clickPauseMs);
            if (!moved)
            {
                logger.LogWarning("Could not reach requested startPage={StartPage}. Stopped at page {CurrentPage}.", requestedStartPage, currentPage);
                return offersByUrl.Values.Take(targetCount).ToList();
            }

            currentPage++;
        }

        while (currentPage <= requestedEndPage && offersByUrl.Count < targetCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForLinkedInResultsReadyAsync(page, resultLoadTimeoutMs);

            logger.LogInformation("LinkedIn direct scraping step 3/4: selecting first list item with href '/jobs/view/'.");
            await ClickFirstLinkedInResultAsync(page, clickPauseMs, detailLoadTimeoutMs);

            var previousVisibleCount = 0;
            for (var attempt = 0; attempt < maxScrollAttemptsPerPage && offersByUrl.Count < targetCount; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var items = page.Locator("li[data-occludable-job-id]");
                var itemCount = await items.CountAsync();
                if (itemCount == 0)
                {
                    await page.WaitForTimeoutAsync(scrollPauseMs);
                    continue;
                }

                for (var index = 0; index < itemCount && offersByUrl.Count < targetCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var item = items.Nth(index);
                    var rawJobId = await item.GetAttributeAsync("data-occludable-job-id") ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(rawJobId) && seenJobIds.Contains(rawJobId))
                    {
                        continue;
                    }

                    var clickable = item.Locator("a[href*='/jobs/view/']").First;
                    if (await clickable.CountAsync() == 0)
                    {
                        continue;
                    }

                    var cardHref = await clickable.GetAttributeAsync("href") ?? string.Empty;
                    var cardTitle = (await clickable.InnerTextAsync()).Trim();
                    var cardCompany = await ReadLocatorTextOrDefaultAsync(
                        item,
                        ".artdeco-entity-lockup__subtitle span, .artdeco-entity-lockup__subtitle, .job-card-container__primary-description");
                    var cardLocation = await ReadLocatorTextOrDefaultAsync(
                        item,
                        ".job-card-container__metadata-wrapper li span, .artdeco-entity-lockup__caption span");

                    await clickable.ClickAsync();
                    await page.WaitForTimeoutAsync(clickPauseMs);

                    logger.LogDebug("LinkedIn direct scraping step 4/4: extracting details wrapper for list index {Index}.", index);
                    var details = await ExtractLinkedInJobDetailsAsync(page, detailLoadTimeoutMs);

                    var finalUrl = NormalizeLinkedInJobUrl(details?.Url ?? cardHref);
                    var finalTitle = NormalizeLinkedInTitle(string.IsNullOrWhiteSpace(details?.Title) ? cardTitle : details!.Title!);
                    var finalCompany = NormalizeLinkedInText(string.IsNullOrWhiteSpace(details?.Company) ? cardCompany : details!.Company!);
                    var finalLocation = NormalizeLinkedInText(string.IsNullOrWhiteSpace(details?.Location) ? cardLocation : details!.Location!);
                    var finalDescription = string.IsNullOrWhiteSpace(details?.Description)
                        ? (string.IsNullOrWhiteSpace(finalTitle) ? "LinkedIn job" : finalTitle)
                        : NormalizeLinkedInText(details!.Description!);

                    if (string.IsNullOrWhiteSpace(finalUrl) || string.IsNullOrWhiteSpace(finalTitle))
                    {
                        logger.LogWarning("LinkedIn card skipped because required fields were missing after detail extraction. Index={Index} JobId={JobId}", index, rawJobId);
                        continue;
                    }

                    logger.LogInformation(
                        "LinkedIn detail extracted. Page={Page} Index={Index} JobId={JobId} Title={Title} Company={Company}",
                        currentPage,
                        index,
                        rawJobId,
                        finalTitle,
                        string.IsNullOrWhiteSpace(finalCompany) ? "Unknown" : finalCompany);

                    var cleanUrl = finalUrl;
                    if (offersByUrl.ContainsKey(cleanUrl))
                    {
                        if (!string.IsNullOrWhiteSpace(rawJobId))
                        {
                            seenJobIds.Add(rawJobId);
                        }

                        continue;
                    }

                    var externalIdSeed = !string.IsNullOrWhiteSpace(rawJobId) ? rawJobId : cleanUrl.Trim().ToLowerInvariant();
                    var metadata = JsonSerializer.Serialize(new
                    {
                        origin = "linkedin-direct",
                        page = currentPage,
                        rank = index + 1,
                        jobId = rawJobId
                    });

                    offersByUrl[cleanUrl] = new JobOffer
                    {
                        ExternalId = ComputeShortHash(externalIdSeed),
                        Title = finalTitle,
                        Company = string.IsNullOrWhiteSpace(finalCompany) ? "Unknown" : finalCompany,
                        Location = string.IsNullOrWhiteSpace(finalLocation) ? NormalizeLinkedInText(normalizedLocation) : finalLocation,
                        Description = finalDescription,
                        Url = cleanUrl,
                        Contact = null,
                        SalaryRange = null,
                        PublishedAt = null,
                        Seniority = null,
                        ContractType = null,
                        Source = "linkedin",
                        SearchTerm = query,
                        CapturedAt = now,
                        MetadataJson = metadata
                    };

                    if (!string.IsNullOrWhiteSpace(rawJobId))
                    {
                        seenJobIds.Add(rawJobId);
                    }
                }

                var visibleCount = await items.CountAsync();
                if (visibleCount <= previousVisibleCount)
                {
                    break;
                }

                previousVisibleCount = visibleCount;
                await ScrollLinkedInResultsAsync(page, scrollPauseMs);
            }

            if (offersByUrl.Count >= targetCount || currentPage >= requestedEndPage)
            {
                break;
            }

            var moved = await GoToNextLinkedInPageAsync(page, clickPauseMs);
            if (!moved)
            {
                break;
            }

            currentPage++;
        }

        return offersByUrl.Values.Take(targetCount).ToList();
    }

    private async Task<List<JobOffer>> SearchByGoogleWithPlaywrightAsync(
        string provider,
        string query,
        string? location,
        int limit,
        CancellationToken cancellationToken)
    {
        var targetCount = Math.Clamp(limit, 1, 100);
        var maxPagesFromLimit = (int)Math.Ceiling(targetCount / 10d);
        var maxPagesFromConfig = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:MaxPagesPerSearch") ?? 5, 1, 20);
        var maxPages = Math.Min(maxPagesFromLimit, maxPagesFromConfig);

        var headless = configuration.GetValue<bool?>("Jobs:Playwright:Headless") ?? true;
        var slowMoMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:SlowMoMs") ?? 0, 0, 5000);
        var navigationTimeoutMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:NavigationTimeoutMs") ?? 30000, 5000, 120000);
        var delayBetweenPagesMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:DelayBetweenPagesMs") ?? 1500, 0, 10000);

        var normalizedLocation = string.IsNullOrWhiteSpace(location) ? "Remote" : location.Trim();
        var searchQuery = BuildGoogleQuery(provider, query, normalizedLocation);
        var now = DateTime.UtcNow;
        var offersByUrl = new Dictionary<string, JobOffer>(StringComparer.OrdinalIgnoreCase);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
            SlowMo = slowMoMs
        });

        var contextOptions = new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        };

        if (provider == "linkedin")
        {
            var storagePath = GetStorageStatePath("linkedin");
            if (!File.Exists(storagePath))
            {
                throw new InvalidOperationException("LinkedIn session not found. Login first using /api/auth/login");
            }

            contextOptions.StorageStatePath = storagePath;
        }

        var context = await browser.NewContextAsync(contextOptions);

        var page = await context.NewPageAsync();
        page.SetDefaultNavigationTimeout(navigationTimeoutMs);

        for (var pageIndex = 0; pageIndex < maxPages && offersByUrl.Count < targetCount; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var start = pageIndex * 10;
            var url = $"https://www.google.com/search?q={Uri.EscapeDataString(searchQuery)}&num=10&start={start}&hl=en";
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            await page.WaitForTimeoutAsync(700);
            var pageResults = await ExtractGoogleResultsAsync(page);

            if (pageResults.Count == 0)
            {
                logger.LogInformation("No more results for provider {Provider} at page {Page}", provider, pageIndex + 1);
                break;
            }

            var rankInPage = 0;
            foreach (var result in pageResults)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(result.Url) || string.IsNullOrWhiteSpace(result.Title))
                {
                    continue;
                }

                if (!IsAcceptedResultForProvider(provider, result.Url))
                {
                    continue;
                }

                rankInPage++;
                if (offersByUrl.ContainsKey(result.Url))
                {
                    continue;
                }

                var externalId = ComputeShortHash(result.Url.Trim().ToLowerInvariant());
                var company = ExtractCompanyFromTitle(result.Title);
                var metadata = JsonSerializer.Serialize(new
                {
                    origin = "google-playwright",
                    page = pageIndex + 1,
                    rank = rankInPage
                });

                offersByUrl[result.Url] = new JobOffer
                {
                    ExternalId = externalId,
                    Title = result.Title.Trim(),
                    Company = company,
                    Location = normalizedLocation,
                    Description = string.IsNullOrWhiteSpace(result.Snippet) ? result.Title.Trim() : result.Snippet.Trim(),
                    Url = result.Url.Trim(),
                    Contact = null,
                    SalaryRange = null,
                    PublishedAt = null,
                    Seniority = null,
                    ContractType = null,
                    Source = provider,
                    SearchTerm = query,
                    CapturedAt = now,
                    MetadataJson = metadata
                };

                if (offersByUrl.Count >= targetCount)
                {
                    break;
                }
            }

            if (delayBetweenPagesMs > 0 && pageIndex < maxPages - 1)
            {
                await page.WaitForTimeoutAsync(delayBetweenPagesMs);
            }
        }

        return offersByUrl.Values.Take(targetCount).ToList();
    }

    private async Task LoginLinkedInAndPersistSessionAsync(string username, string password, CancellationToken cancellationToken)
    {
        var headless = configuration.GetValue<bool?>("Jobs:Playwright:LoginHeadless") ?? false;
        var navigationTimeoutMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:NavigationTimeoutMs") ?? 30000, 5000, 120000);
        var manualChallengeTimeoutSeconds = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:ManualChallengeTimeoutSeconds") ?? 180, 30, 900);
        var storagePath = GetStorageStatePath("linkedin");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
            SlowMo = 50
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        });

        var page = await context.NewPageAsync();
        page.SetDefaultNavigationTimeout(navigationTimeoutMs);
        cancellationToken.ThrowIfCancellationRequested();

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
        await page.WaitForTimeoutAsync(3500);
        cancellationToken.ThrowIfCancellationRequested();

        var currentUrl = page.Url.ToLowerInvariant();
        if (currentUrl.Contains("/checkpoint") || currentUrl.Contains("/challenge"))
        {
            if (headless)
            {
                throw new InvalidOperationException(
                    "LinkedIn requested checkpoint/challenge while LoginHeadless=true. " +
                    "In Docker this is expected because manual verification is not possible in headless mode. " +
                    "Complete login once in a non-headless environment (local backend with Jobs:Playwright:LoginHeadless=false), " +
                    "finish verification manually, and reuse the generated playwright-state/linkedin.json file.");
            }

            logger.LogInformation("LinkedIn challenge detected. Waiting up to {Seconds}s for manual completion...", manualChallengeTimeoutSeconds);

            var deadline = DateTime.UtcNow.AddSeconds(manualChallengeTimeoutSeconds);
            var solved = false;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await page.WaitForTimeoutAsync(2000);

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
                throw new InvalidOperationException(
                    "LinkedIn challenge was not completed before timeout. Increase Jobs:Playwright:ManualChallengeTimeoutSeconds " +
                    "or retry and finish the challenge in the opened browser window.");
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

        var directory = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(storagePath, storageState, cancellationToken);
    }

    private static string BuildLinkedInJobsSearchUrl(string query, string location)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var encodedLocation = Uri.EscapeDataString(location);
        return $"https://www.linkedin.com/jobs/search/?keywords={encodedQuery}&location={encodedLocation}";
    }

    private static bool IsLinkedInAuthRedirect(string url)
    {
        var current = url.ToLowerInvariant();
        return current.Contains("/login") || current.Contains("/checkpoint") || current.Contains("/challenge");
    }

    private async Task ApplyLinkedInSearchInputAsync(IPage page, string query, int clickPauseMs)
    {
        var input = page.Locator("input[data-testid='typeahead-input'], input[placeholder='Search'], input[aria-label*='Search' i], input[aria-autocomplete='list'], input[id^=':r']").First;
        if (await input.CountAsync() == 0)
        {
            logger.LogWarning("LinkedIn search input was not found. Continuing with URL-based search params.");
            return;
        }

        try
        {
            await input.ClickAsync();
            await input.FillAsync(query);
            await input.PressAsync("Enter");
            await page.WaitForTimeoutAsync(clickPauseMs);
        }
        catch (PlaywrightException ex)
        {
            logger.LogWarning(ex, "LinkedIn search input interaction failed. Continuing with URL-based search params.");
        }
    }

    private async Task WaitForLinkedInSearchPageAsync(IPage page, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (page.Url.Contains("/jobs/search/?", StringComparison.OrdinalIgnoreCase) ||
                page.Url.Contains("/jobs/search", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await page.WaitForTimeoutAsync(300);
        }

        throw new TimeoutException("LinkedIn jobs search page did not load in expected time.");
    }

    private async Task ClickFirstLinkedInResultAsync(IPage page, int clickPauseMs, int detailLoadTimeoutMs)
    {
        var firstLink = page.Locator("li[data-occludable-job-id] a[href*='/jobs/view/']").First;
        if (await firstLink.CountAsync() == 0)
        {
            throw new InvalidOperationException("LinkedIn first job item was not found in results list.");
        }

        await firstLink.ClickAsync();
        await page.WaitForTimeoutAsync(clickPauseMs);

        var wrapper = page.Locator(".jobs-search__job-details--wrapper").First;
        await wrapper.WaitForAsync(new LocatorWaitForOptions
        {
            Timeout = detailLoadTimeoutMs,
            State = WaitForSelectorState.Visible
        });
    }

    private async Task WaitForLinkedInResultsReadyAsync(IPage page, int timeoutMs)
    {
        await page.WaitForSelectorAsync("div[data-results-list-top-scroll-sentinel]", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Attached
        });

        await page.WaitForSelectorAsync("li[data-occludable-job-id]", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Attached
        });

        await page.WaitForSelectorAsync("li[data-occludable-job-id] a[href*='/jobs/view/']", new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Attached
        });
    }

    private async Task ScrollLinkedInResultsAsync(IPage page, int scrollPauseMs)
    {
        await page.EvaluateAsync("""
            () => {
                const list = document.querySelector('div[data-results-list-top-scroll-sentinel]')?.parentElement;
                const container = list?.closest('div.scaffold-layout__list') || list || document.scrollingElement;
                if (!container) return;
                container.scrollBy(0, 900);
            }
            """);

        await page.WaitForTimeoutAsync(scrollPauseMs);
    }

    private async Task<bool> GoToNextLinkedInPageAsync(IPage page, int clickPauseMs)
    {
        var nextButton = page.Locator("button.jobs-search-pagination__button--next, button[aria-label*='Siguiente' i], button[aria-label*='next' i]").First;
        if (await nextButton.CountAsync() == 0 || !await nextButton.IsVisibleAsync())
        {
            return false;
        }

        if (!await nextButton.IsEnabledAsync())
        {
            return false;
        }

        await nextButton.ClickAsync();
        await page.WaitForTimeoutAsync(clickPauseMs);
        return true;
    }

    private async Task<LinkedInJobDetails?> ExtractLinkedInJobDetailsAsync(IPage page, int timeoutMs)
    {
        var wrapper = page.Locator(".jobs-search__job-details--wrapper").First;
        if (await wrapper.CountAsync() == 0)
        {
            return null;
        }

        await wrapper.WaitForAsync(new LocatorWaitForOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Visible
        });

        var detailContent = page.Locator("#job-details, .jobs-description__content, .jobs-box__html-content").First;
        if (await detailContent.CountAsync() > 0)
        {
            await detailContent.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = timeoutMs,
                State = WaitForSelectorState.Attached
            });
        }

        await page.WaitForTimeoutAsync(450);

        var data = await page.EvaluateAsync<string>("""
            () => {
                const root = document.querySelector('.jobs-search__job-details--wrapper');
                if (!root) return JSON.stringify({});

                const text = (selectorList) => {
                    for (const selector of selectorList) {
                        const el = root.querySelector(selector);
                        if (el && el.textContent && el.textContent.trim()) {
                            return el.textContent.trim();
                        }
                    }
                    return '';
                };

                const title = text([
                    '.job-details-jobs-unified-top-card__job-title h1 a',
                    '.job-details-jobs-unified-top-card__job-title h1',
                    '.job-details-jobs-unified-top-card__job-title',
                    'h1'
                ]);
                const company = text([
                    '.job-details-jobs-unified-top-card__company-name a',
                    '.job-details-jobs-unified-top-card__company-name'
                ]);
                const location = text([
                    '.job-details-jobs-unified-top-card__primary-description-container span',
                    '.job-details-jobs-unified-top-card__primary-description-without-tagline span'
                ]);
                const description = '';

                let url = window.location.href || '';
                const canonical = document.querySelector("link[rel='canonical']")?.getAttribute('href');
                if (canonical && canonical.includes('/jobs/view/')) {
                    url = canonical;
                }

                if (!url || !url.includes('/jobs/view/')) {
                    const titleLink = root.querySelector('.job-details-jobs-unified-top-card__job-title h1 a')?.getAttribute('href') || '';
                    if (titleLink) {
                        url = titleLink;
                    }
                }

                if (!url || !url.includes('/jobs/view/')) {
                    const detailLink = root.querySelector("a[href*='/jobs/view/']")?.getAttribute('href') || '';
                    if (detailLink) {
                        url = detailLink;
                    }
                }

                return JSON.stringify({ title, company, location, description, url });
            }
            """);

        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        var details = JsonSerializer.Deserialize<LinkedInJobDetails>(data);
        if (details is null)
        {
            return null;
        }

        var longDescription = await ExtractLinkedInLongDescriptionAsync(page, timeoutMs);
        if (!string.IsNullOrWhiteSpace(longDescription))
        {
            return details with { Description = longDescription };
        }

        return details;
    }

    private async Task<string> ExtractLinkedInLongDescriptionAsync(IPage page, int timeoutMs)
    {
        var showMore = page.Locator("#job-details .inline-show-more-text__button, .jobs-description .inline-show-more-text__button, button.inline-show-more-text__button").First;
        if (await showMore.CountAsync() > 0 && await showMore.IsVisibleAsync())
        {
            try
            {
                await showMore.ClickAsync();
                await page.WaitForTimeoutAsync(300);
            }
            catch (PlaywrightException)
            {
                // Non-blocking: continue extracting available content.
            }
        }

        var descriptionNode = page.Locator("#job-details p[dir='ltr'], #job-details, .jobs-box__html-content").First;
        if (await descriptionNode.CountAsync() == 0)
        {
            return string.Empty;
        }

        await descriptionNode.WaitForAsync(new LocatorWaitForOptions
        {
            Timeout = timeoutMs,
            State = WaitForSelectorState.Attached
        });

        var text = await descriptionNode.InnerTextAsync();
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
    }

    private static string NormalizeLinkedInJobUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://www.linkedin.com" + (url.StartsWith("/") ? string.Empty : "/") + url;
        }

        var clean = url.Split('?', '#')[0];
        return clean.TrimEnd('/');
    }

    private static string NormalizeLinkedInTitle(string value)
    {
        var normalized = NormalizeLinkedInText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lines = normalized
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => NormalizeLinkedInText(line))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var first = lines[0];
        if (lines.All(line => line.Equals(first, StringComparison.OrdinalIgnoreCase)))
        {
            return first;
        }

        return first;
    }

    private static string NormalizeLinkedInText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        while (normalized.Contains("\n\n\n", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("\n\n\n", "\n\n", StringComparison.Ordinal);
        }

        return normalized;
    }

    private static async Task<string> ReadLocatorTextOrDefaultAsync(ILocator scope, string selector)
    {
        var target = scope.Locator(selector).First;
        if (await target.CountAsync() == 0)
        {
            return string.Empty;
        }

        var text = await target.InnerTextAsync();
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
    }

    private async Task<bool> ValidateLinkedInSessionForScrapingAsync(CancellationToken cancellationToken)
    {
        var storagePath = GetStorageStatePath("linkedin");
        if (!File.Exists(storagePath))
        {
            return false;
        }

        var navigationTimeoutMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:NavigationTimeoutMs") ?? 30000, 5000, 120000);

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                StorageStatePath = storagePath,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
            });

            var page = await context.NewPageAsync();
            page.SetDefaultNavigationTimeout(navigationTimeoutMs);
            await page.GotoAsync("https://www.linkedin.com/feed", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForTimeoutAsync(1200);
            cancellationToken.ThrowIfCancellationRequested();

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
            logger.LogWarning(ex, "Failed to validate LinkedIn session state before scraping.");
            return false;
        }
    }

    private async Task<bool> IsProviderAuthenticatedAsync(string provider, CancellationToken cancellationToken)
    {
        var isAuthenticated = await sessionRepository.IsAuthenticatedAsync(provider, cancellationToken);
        if (!isAuthenticated)
        {
            return false;
        }

        if (provider == "linkedin")
        {
            var storagePath = GetStorageStatePath("linkedin");
            return File.Exists(storagePath);
        }

        return true;
    }

    private string GetStorageStatePath(string provider)
    {
        var configuredDirectory = configuration.GetValue<string>("Jobs:Playwright:SessionStateDirectory");
        var baseDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "playwright-state")
            : Path.GetFullPath(configuredDirectory);

        return Path.Combine(baseDirectory, $"{provider}.json");
    }

    private static string BuildGoogleQuery(string provider, string query, string location)
    {
        return provider switch
        {
            "linkedin" => $"site:linkedin.com/jobs/view \"{query}\" \"{location}\"",
            "indeed" => $"site:indeed.com/viewjob \"{query}\" \"{location}\"",
            _ => $"\"{query}\" \"{location}\""
        };
    }

    private static async Task<List<GoogleSearchResult>> ExtractGoogleResultsAsync(IPage page)
    {
        var json = await page.EvaluateAsync<string>(@"() => {
            const rows = [...document.querySelectorAll('div#search div.g')];
            const data = rows.map((row) => {
                const link = row.querySelector('a')?.href || '';
                const title = row.querySelector('h3')?.innerText || '';
                const snippet = row.querySelector('div.VwiC3b, span.aCOpRe')?.innerText || '';
                return { title, url: link, snippet };
            }).filter(x => x.url && x.title);
            return JSON.stringify(data);
        }");

        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<GoogleSearchResult>>(json) ?? [];
    }

    private static bool IsAcceptedResultForProvider(string provider, string url)
    {
        var normalized = url.ToLowerInvariant();
        return provider switch
        {
            "linkedin" => normalized.Contains("linkedin.com/jobs/view"),
            "indeed" => normalized.Contains("indeed.com/viewjob"),
            _ => false
        };
    }

    private static string ExtractCompanyFromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Unknown";
        }

        var separators = new[] { " - ", " | ", " at " };
        foreach (var separator in separators)
        {
            var parts = title.Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return parts[1].Length > 220 ? parts[1][..220] : parts[1];
            }
        }

        return "Unknown";
    }

    private static string ComputeShortHash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..10].ToLowerInvariant();
    }

    private sealed record GoogleSearchResult(string Title, string Url, string? Snippet);

    private sealed record LinkedInJobDetails(string? Title, string? Company, string? Location, string? Description, string? Url);
}
