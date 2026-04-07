using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using backend.Application.Contracts;
using Microsoft.Playwright;

namespace backend.Infrastructure.Scraping;

public sealed class PlaywrightScraper(
    IBrowserPool browserPool,
    IConfiguration configuration,
    ILogger<PlaywrightScraper> logger) : IPlaywrightScraper
{
    public async Task<List<RawJobData>> ScrapeLinkedInAsync(SearchRequest request, string storageStatePath, CancellationToken ct)
    {
        if (!File.Exists(storageStatePath))
        {
            throw new InvalidOperationException("LinkedIn session not found. Login first using /api/auth/login");
        }

        var normalizedLocation = string.IsNullOrWhiteSpace(request.Location) ? "Remote" : request.Location.Trim();
        var navigationTimeoutMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:NavigationTimeoutMs") ?? 30000, 5000, 120000);
        var resultLoadTimeoutMs = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:ResultLoadTimeoutMs") ?? 20000, 5000, 120000);
        var detailLoadTimeoutMs = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:DetailLoadTimeoutMs") ?? 12000, 3000, 60000);
        var scrollPauseMs = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:ScrollPauseMs") ?? 1200, 200, 10000);
        var clickPauseMs = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:ClickPauseMs") ?? 600, 100, 5000);
        var maxScrollAttemptsPerPage = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:MaxScrollAttemptsPerPage") ?? 8, 1, 50);
        var maxPages = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:MaxPages") ?? 5, 1, 50);
        var estimatedJobsPerPage = Math.Clamp(configuration.GetValue<int?>("Jobs:LinkedIn:DirectScraping:EstimatedJobsPerPage") ?? 25, 1, 50);

        var requestedStartPage = Math.Clamp(request.StartPage ?? 1, 1, maxPages);
        var requestedPaging = Math.Clamp(request.TotalPaging ?? 1, 1, maxPages);
        var requestedEndPage = request.EndPage.HasValue
            ? Math.Clamp(request.EndPage.Value, requestedStartPage, maxPages)
            : Math.Min(maxPages, requestedStartPage + requestedPaging - 1);
        var pagesToProcess = Math.Max(1, requestedEndPage - requestedStartPage + 1);
        var pagingExpectedJobs = pagesToProcess * estimatedJobsPerPage;
        var targetCount = Math.Clamp(Math.Max(request.Limit, pagingExpectedJobs), 1, 5000);

        logger.LogInformation(
            "LinkedIn paging request resolved to range {StartPage}..{EndPage} (requested totalPaging={TotalPaging}, limit={Limit}).",
            requestedStartPage,
            requestedEndPage,
            request.TotalPaging,
            request.Limit);

        var now = DateTime.UtcNow;
        var resultsByUrl = new Dictionary<string, RawJobData>(StringComparer.OrdinalIgnoreCase);
        var seenJobIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var browser = await browserPool.GetBrowserAsync(ct);
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            StorageStatePath = storageStatePath,
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        });

        var page = await context.NewPageAsync();
        page.SetDefaultNavigationTimeout(navigationTimeoutMs);

        await page.GotoAsync(BuildLinkedInJobsSearchUrl(request.Query, normalizedLocation), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        if (IsLinkedInAuthRedirect(page.Url))
        {
            throw new InvalidOperationException("LinkedIn session expired. Re-authenticate with POST /api/auth/login.");
        }

        await ApplyLinkedInSearchInputAsync(page, request.Query, clickPauseMs);
        await WaitForLinkedInSearchPageAsync(page, resultLoadTimeoutMs);

        var currentPage = 1;
        while (currentPage < requestedStartPage)
        {
            ct.ThrowIfCancellationRequested();
            await WaitForLinkedInResultsReadyAsync(page, resultLoadTimeoutMs);
            var moved = await GoToNextLinkedInPageAsync(page, clickPauseMs);
            if (!moved)
            {
                logger.LogWarning("Could not reach requested startPage={StartPage}. Stopped at page {CurrentPage}.", requestedStartPage, currentPage);
                return resultsByUrl.Values.Take(targetCount).ToList();
            }

            currentPage++;
        }

        while (currentPage <= requestedEndPage && resultsByUrl.Count < targetCount)
        {
            ct.ThrowIfCancellationRequested();
            await WaitForLinkedInResultsReadyAsync(page, resultLoadTimeoutMs);
            await ClickFirstLinkedInResultAsync(page, clickPauseMs, detailLoadTimeoutMs);

            var previousVisibleCount = 0;
            for (var attempt = 0; attempt < maxScrollAttemptsPerPage && resultsByUrl.Count < targetCount; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                var items = page.Locator("li[data-occludable-job-id]");
                var itemCount = await items.CountAsync();
                if (itemCount == 0)
                {
                    await page.WaitForTimeoutAsync(scrollPauseMs);
                    continue;
                }

                for (var index = 0; index < itemCount && resultsByUrl.Count < targetCount; index++)
                {
                    ct.ThrowIfCancellationRequested();

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
                        continue;
                    }

                    if (resultsByUrl.ContainsKey(finalUrl))
                    {
                        if (!string.IsNullOrWhiteSpace(rawJobId))
                        {
                            seenJobIds.Add(rawJobId);
                        }

                        continue;
                    }

                    var externalIdSeed = !string.IsNullOrWhiteSpace(rawJobId) ? rawJobId : finalUrl.Trim().ToLowerInvariant();
                    var metadata = JsonSerializer.Serialize(new
                    {
                        origin = "linkedin-direct",
                        page = currentPage,
                        rank = index + 1,
                        jobId = rawJobId
                    });

                    resultsByUrl[finalUrl] = new RawJobData(
                        ExternalKey: externalIdSeed,
                        Title: finalTitle,
                        Company: string.IsNullOrWhiteSpace(finalCompany) ? "Unknown" : finalCompany,
                        Location: string.IsNullOrWhiteSpace(finalLocation) ? NormalizeLinkedInText(normalizedLocation) : finalLocation,
                        Description: finalDescription,
                        Url: finalUrl,
                        Source: "linkedin",
                        SearchTerm: request.Query,
                        CapturedAt: now,
                        MetadataJson: metadata);

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

            if (resultsByUrl.Count >= targetCount || currentPage >= requestedEndPage)
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

        return resultsByUrl.Values.Take(targetCount).ToList();
    }

    public async Task<List<RawJobData>> ScrapeGoogleAsync(string provider, SearchRequest request, CancellationToken ct)
    {
        var targetCount = Math.Clamp(request.Limit, 1, 100);
        var maxPagesFromLimit = (int)Math.Ceiling(targetCount / 10d);
        var maxPagesFromConfig = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:MaxPagesPerSearch") ?? 5, 1, 20);
        var maxPages = Math.Min(maxPagesFromLimit, maxPagesFromConfig);

        var navigationTimeoutMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:NavigationTimeoutMs") ?? 30000, 5000, 120000);
        var delayBetweenPagesMs = Math.Clamp(configuration.GetValue<int?>("Jobs:Playwright:DelayBetweenPagesMs") ?? 1500, 0, 10000);

        var normalizedLocation = string.IsNullOrWhiteSpace(request.Location) ? "Remote" : request.Location.Trim();
        var searchQuery = BuildGoogleQuery(provider, request.Query, normalizedLocation);
        var now = DateTime.UtcNow;

        var resultsByUrl = new Dictionary<string, RawJobData>(StringComparer.OrdinalIgnoreCase);
        var browser = await browserPool.GetBrowserAsync(ct);
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        });

        var page = await context.NewPageAsync();
        page.SetDefaultNavigationTimeout(navigationTimeoutMs);

        for (var pageIndex = 0; pageIndex < maxPages && resultsByUrl.Count < targetCount; pageIndex++)
        {
            ct.ThrowIfCancellationRequested();

            var start = pageIndex * 10;
            var url = $"https://www.google.com/search?q={Uri.EscapeDataString(searchQuery)}&num=10&start={start}&hl=en";
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            await page.WaitForTimeoutAsync(700);
            var pageResults = await ExtractGoogleResultsAsync(page);

            if (pageResults.Count == 0)
            {
                break;
            }

            var rankInPage = 0;
            foreach (var result in pageResults)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(result.Url) || string.IsNullOrWhiteSpace(result.Title))
                {
                    continue;
                }

                if (!IsAcceptedResultForProvider(provider, result.Url))
                {
                    continue;
                }

                rankInPage++;
                if (resultsByUrl.ContainsKey(result.Url))
                {
                    continue;
                }

                var metadata = JsonSerializer.Serialize(new
                {
                    origin = "google-playwright",
                    page = pageIndex + 1,
                    rank = rankInPage
                });

                resultsByUrl[result.Url] = new RawJobData(
                    ExternalKey: result.Url.Trim().ToLowerInvariant(),
                    Title: result.Title.Trim(),
                    Company: ExtractCompanyFromTitle(result.Title),
                    Location: normalizedLocation,
                    Description: string.IsNullOrWhiteSpace(result.Snippet) ? result.Title.Trim() : result.Snippet.Trim(),
                    Url: result.Url.Trim(),
                    Source: provider,
                    SearchTerm: request.Query,
                    CapturedAt: now,
                    MetadataJson: metadata);

                if (resultsByUrl.Count >= targetCount)
                {
                    break;
                }
            }

            if (delayBetweenPagesMs > 0 && pageIndex < maxPages - 1)
            {
                await page.WaitForTimeoutAsync(delayBetweenPagesMs);
            }
        }

        return resultsByUrl.Values.Take(targetCount).ToList();
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

    private static string BuildGoogleQuery(string provider, string query, string location)
    {
        return provider switch
        {
            "linkedin" => $"site:linkedin.com/jobs/view \"{query}\" \"{location}\"",
            "indeed" => $"site:indeed.com/viewjob \"{query}\" \"{location}\"",
            _ => $"\"{query}\" \"{location}\""
        };
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

    private static async Task WaitForLinkedInSearchPageAsync(IPage page, int timeoutMs)
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

    private static async Task ClickFirstLinkedInResultAsync(IPage page, int clickPauseMs, int detailLoadTimeoutMs)
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

    private static async Task WaitForLinkedInResultsReadyAsync(IPage page, int timeoutMs)
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

    private static async Task ScrollLinkedInResultsAsync(IPage page, int scrollPauseMs)
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

    private static async Task<bool> GoToNextLinkedInPageAsync(IPage page, int clickPauseMs)
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

    private static async Task<LinkedInJobDetails?> ExtractLinkedInJobDetailsAsync(IPage page, int timeoutMs)
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

    private static async Task<string> ExtractLinkedInLongDescriptionAsync(IPage page, int timeoutMs)
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
                // Continue with available content.
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

        return lines[0];
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

    private static string ComputeShortHash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..10].ToLowerInvariant();
    }

    private sealed record GoogleSearchResult(string Title, string Url, string? Snippet);

    private sealed record LinkedInJobDetails(string? Title, string? Company, string? Location, string? Description, string? Url);
}
