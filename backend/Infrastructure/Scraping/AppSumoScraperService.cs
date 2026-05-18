using backend.Domain.Entities;
using Microsoft.Playwright;
using Polly;
using Polly.Retry;

namespace backend.Infrastructure.Scraping;

/// <summary>
/// Navigates AppSumo categories → products → reviews (1–3 tacos only).
/// Selectors derived from real AppSumo HTML snapshots.
/// </summary>
public sealed class AppSumoScraperService(
    IBrowserPool browserPool,
    IConfiguration configuration,
    ILogger<AppSumoScraperService> logger)
{
    private const string BaseUrl = "https://appsumo.com";
    private static readonly int[] TargetRatings = [3, 2, 1];

    private readonly int _minDelayMs = configuration.GetValue<int?>("AppSumoScraper:MinDelayMs") ?? 1800;
    private readonly int _maxDelayMs = configuration.GetValue<int?>("AppSumoScraper:MaxDelayMs") ?? 4500;

    private readonly AsyncRetryPolicy _retryPolicy = Policy
        .Handle<PlaywrightException>()
        .Or<TimeoutException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt =>
                TimeSpan.FromSeconds(Math.Pow(2, attempt)) +
                TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)));

    // ── Public surface ────────────────────────────────────────────────────────

    public async Task<List<AppSumoCategory>> ExtractCategoriesAsync(CancellationToken ct)
    {
        logger.LogInformation("AppSumo: extracting categories from {Url}/software/", BaseUrl);
        var browser = await browserPool.GetBrowserAsync(ct);
        var context = await browser.NewContextAsync(BuildContextOptions());
        var page    = await context.NewPageAsync();
        try
        {
            await ApplyStealthAsync(page);
            await _retryPolicy.ExecuteAsync(() =>
                page.GotoAsync($"{BaseUrl}/software/",
                    new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30_000 }));

            return await ParseCategoriesAsync(page);
        }
        finally { await context.CloseAsync(); }
    }

    public async Task<List<AppSumoProduct>> ExtractProductsForCategoryAsync(
        AppSumoCategory category,
        int maxProducts,
        CancellationToken ct)
    {
        logger.LogInformation("AppSumo: scraping products for category {Slug}", category.Slug);
        var browser = await browserPool.GetBrowserAsync(ct);
        var context = await browser.NewContextAsync(BuildContextOptions());
        var page    = await context.NewPageAsync();
        try
        {
            await ApplyStealthAsync(page);
            var products = new List<AppSumoProduct>();
            var pageNum  = 1;

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var url = pageNum == 1
                    ? $"{BaseUrl}{category.Url}"
                    : $"{BaseUrl}{category.Url}?page={pageNum}";

                logger.LogInformation("AppSumo: fetching product listing {Url}", url);
                await _retryPolicy.ExecuteAsync(() =>
                    page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30_000 }));

                logger.LogInformation("AppSumo: page loaded, current URL = {CurrentUrl}", page.Url);

                // Wait for product links to appear
                try { await page.WaitForSelectorAsync("a[href^='/products/']", new() { Timeout = 10_000 }); }
                catch (Exception ex)
                {
                    logger.LogWarning("AppSumo: no product links found on {Url} — {Err}", url, ex.Message);
                    break;
                }

                var pageProducts = await ParseProductsFromListingAsync(page, category);
                logger.LogInformation("AppSumo: parsed {Count} products on page {Page}", pageProducts.Count, pageNum);
                if (pageProducts.Count == 0) break;

                products.AddRange(pageProducts);
                logger.LogInformation("AppSumo: category {Slug} page {Page} → {Count} products",
                    category.Slug, pageNum, pageProducts.Count);

                if (maxProducts > 0 && products.Count >= maxProducts) break;

                // AppSumo uses page query param; check if a next page exists by looking for pagination
                var nextBtn = page.Locator("a[aria-label='Next page'], a[rel='next'], button[aria-label='Next']");
                if (await nextBtn.CountAsync() == 0) break;

                pageNum++;
                await HumanDelayAsync(ct);
            }

            return maxProducts > 0 ? [.. products.Take(maxProducts)] : products;
        }
        finally { await context.CloseAsync(); }
    }

    public async Task<List<AppSumoReview>> ExtractLowRatingReviewsAsync(
        AppSumoProduct product,
        CancellationToken ct)
    {
        logger.LogInformation("AppSumo: extracting reviews for {Slug}", product.Slug);
        var browser = await browserPool.GetBrowserAsync(ct);
        var context = await browser.NewContextAsync(BuildContextOptions());
        var page    = await context.NewPageAsync();
        try
        {
            await ApplyStealthAsync(page);

            var reviews = new List<AppSumoReview>();
            foreach (var rating in TargetRatings)
            {
                ct.ThrowIfCancellationRequested();

                // Navigate directly to the product page filtered by taco rating via URL param
                await _retryPolicy.ExecuteAsync(() =>
                    page.GotoAsync($"{product.Url}?taco_rating={rating}",
                        new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30_000 }));

                if (await IsBlockedAsync(page))
                {
                    logger.LogWarning("AppSumo: bot challenge detected for {Slug}", product.Slug);
                    throw new InvalidOperationException("Bot challenge detected");
                }

                // Click the rating filter button: [data-testid="taco-rating-{n}-button"]
                var filterBtn = page.Locator($"[data-testid='taco-rating-{rating}-button']");
                if (await filterBtn.CountAsync() > 0)
                {
                    await filterBtn.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    await Task.Delay(Random.Shared.Next(800, 1500), ct);
                }

                var ratingReviews = await ScrapeAllReviewPagesAsync(page, product, (byte)rating, ct);
                reviews.AddRange(ratingReviews);

                logger.LogInformation("AppSumo: {Slug} rating={Rating} → {Count} reviews", product.Slug, rating, ratingReviews.Count);
                await Task.Delay(Random.Shared.Next(800, 2000), ct);
            }

            return reviews;
        }
        finally { await context.CloseAsync(); }
    }

    // ── Category parsing ──────────────────────────────────────────────────────

    private async Task<List<AppSumoCategory>> ParseCategoriesAsync(IPage page)
    {
        var categories = new List<AppSumoCategory>();

        // AppSumo sidebar/nav links to /software/* sub-categories
        var links = await page.Locator("nav a[href^='/software/'], aside a[href^='/software/'], a[href^='/software/']").AllAsync();

        var seen = new HashSet<string>();
        foreach (var link in links)
        {
            var href = await link.GetAttributeAsync("href") ?? string.Empty;
            var text = (await link.InnerTextAsync()).Trim();

            if (string.IsNullOrWhiteSpace(href) || href == "/software/" || string.IsNullOrWhiteSpace(text))
                continue;

            var slug = href.TrimStart('/').TrimEnd('/');
            if (!seen.Add(slug)) continue;

            var parts      = slug.Split('/');
            var parentSlug = parts.Length > 2 ? string.Join('/', parts[..^1]) : null;

            categories.Add(new AppSumoCategory
            {
                Name       = text,
                Slug       = slug,
                Url        = href,
                ParentSlug = parentSlug,
                CreatedAt  = DateTime.UtcNow
            });
        }

        logger.LogInformation("AppSumo: found {Count} categories", categories.Count);
        return categories;
    }

    // ── Product listing parsing ───────────────────────────────────────────────

    private async Task<List<AppSumoProduct>> ParseProductsFromListingAsync(IPage page, AppSumoCategory category)
    {
        var products = new List<AppSumoProduct>();

        // Each product card has an invisible overlay <a href="/products/SLUG/" class="absolute h-full w-full ...">
        // with a <span class="sr-only">PRODUCT NAME</span> inside.
        // We collect all unique product hrefs, avoiding duplicates from multiple link elements per card.
        var links = await page.Locator("a[href^='/products/']").AllAsync();

        var seen = new HashSet<string>();
        foreach (var link in links)
        {
            try
            {
                var href = (await link.GetAttributeAsync("href") ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(href)) continue;

                // Strip hash fragment (#reviews, etc.) before parsing
                var cleanHref = href.Contains('#') ? href[..href.IndexOf('#')] : href;
                var segments  = cleanHref.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var slug      = segments.LastOrDefault() ?? cleanHref;
                if (string.IsNullOrWhiteSpace(slug) || slug == "products") continue;
                if (!seen.Add(slug)) continue;

                // Product name is in the sr-only span inside the overlay link
                var name = (await TryGetTextAsync(link, "span.sr-only") ?? slug).Trim();

                var fullUrl = cleanHref.StartsWith("http") ? cleanHref : $"{BaseUrl}{cleanHref}";

                products.Add(new AppSumoProduct
                {
                    CategoryId = category.Id,
                    Name       = name,
                    Slug       = slug,
                    Url        = fullUrl,
                    CreatedAt  = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "AppSumo: failed to parse product link");
            }
        }

        logger.LogInformation("AppSumo: parsed {Count} products from listing page", products.Count);
        return products;
    }

    // ── Review scraping ───────────────────────────────────────────────────────

    private async Task<List<AppSumoReview>> ScrapeAllReviewPagesAsync(
        IPage page, AppSumoProduct product, byte rating, CancellationToken ct)
    {
        var reviews = new List<AppSumoReview>();

        // AppSumo uses infinite scroll ("scroll-pagination-info") — scroll down to load all reviews
        var prevCount = 0;
        var stallRounds = 0;
        const int maxStalls = 3;

        while (stallRounds < maxStalls)
        {
            ct.ThrowIfCancellationRequested();

            var pageReviews = await ParseReviewCardsAsync(page, product, rating);
            reviews.Clear();
            reviews.AddRange(pageReviews);

            if (reviews.Count == prevCount)
            {
                stallRounds++;
            }
            else
            {
                stallRounds = 0;
                prevCount   = reviews.Count;
            }

            // Check if all reviews loaded
            var paginationInfo = page.Locator("[data-testid='scroll-pagination-info']");
            if (await paginationInfo.CountAsync() > 0)
            {
                var infoText = (await paginationInfo.InnerTextAsync()).Trim();
                // "Showing X of Y" — if X == Y we're done
                if (AllLoaded(infoText)) break;
            }

            // Scroll to bottom to trigger lazy load
            await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
            await Task.Delay(Random.Shared.Next(1200, 2000), ct);
        }

        return reviews;
    }

    private static bool AllLoaded(string infoText)
    {
        // "Showing 12 of 12" or "12 of 12"
        var parts = infoText.Split(["of", "Showing"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2 && int.TryParse(parts[^2].Trim(), out var shown) && int.TryParse(parts[^1].Trim(), out var total))
            return shown >= total;
        return false;
    }

    private async Task<List<AppSumoReview>> ParseReviewCardsAsync(IPage page, AppSumoProduct product, byte rating)
    {
        var reviews = new List<AppSumoReview>();

        // Confirmed selector from real HTML: data-testid="review-card-wrapper"
        var cards = await page.Locator("[data-testid='review-card-wrapper']").AllAsync();

        foreach (var card in cards)
        {
            try
            {
                // Review text: inside [data-testid="discussion-review-info"] → p
                var text = (await TryGetTextAsync(card, "[data-testid='discussion-review-info'] p") ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                // Reviewer name: the bold name link (class="...text-black-pearl") inside user info
                var reviewer = await TryGetTextAsync(card, "a.text-black-pearl");

                // Review date: <span data-testid="creator-posted-date">Jan 21, 2026</span>
                string? reviewId = null;
                DateOnly? reviewDate = null;
                var dateText = await TryGetTextAsync(card, "[data-testid='creator-posted-date']");
                if (!string.IsNullOrWhiteSpace(dateText) && DateOnly.TryParse(dateText.Trim(), out var d))
                    reviewDate = d;

                // Review permalink for dedup: a[href*="/reviews/"] → last path segment
                var permalinkEl = card.Locator("a[href*='/reviews/']").First;
                if (await permalinkEl.CountAsync() > 0)
                {
                    var href  = await permalinkEl.GetAttributeAsync("href") ?? string.Empty;
                    var segs  = href.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    reviewId  = segs.LastOrDefault();
                }

                // Verified purchaser: presence of svg data-icon="circle-check"
                var verifiedEl = card.Locator("svg[data-icon='circle-check']");
                var isVerified = await verifiedEl.CountAsync() > 0;

                // Helpful count: [data-testid="helpful-count"]
                var helpfulText = await TryGetTextAsync(card, "[data-testid='helpful-count']");
                var foundHelpful = int.TryParse(helpfulText?.Trim(), out var hc) ? hc : 0;

                reviews.Add(new AppSumoReview
                {
                    ProductId       = product.Id,
                    AppSumoReviewId = reviewId,
                    TacoRating      = rating,
                    ReviewerName    = reviewer?.Trim(),
                    ReviewDate      = reviewDate,
                    ReviewText      = text,
                    FoundHelpful    = foundHelpful > 0 ? foundHelpful : null,
                    IsVerified      = isVerified,
                    CreatedAt       = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "AppSumo: failed to parse review card");
            }
        }

        return reviews;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task HumanDelayAsync(CancellationToken ct = default)
        => await Task.Delay(Random.Shared.Next(_minDelayMs, _maxDelayMs), ct);

    private static async Task<bool> IsBlockedAsync(IPage page)
    {
        var title = await page.TitleAsync();
        var url   = page.Url;
        return title.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/challenge", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Attention Required", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> TryGetTextAsync(ILocator container, string selector)
    {
        try
        {
            var el = container.Locator(selector).First;
            if (await el.CountAsync() == 0) return null;
            return await el.InnerTextAsync();
        }
        catch { return null; }
    }

    private static BrowserNewContextOptions BuildContextOptions() => new()
    {
        UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
        ViewportSize = new ViewportSize
        {
            Width  = Random.Shared.Next(1280, 1921),
            Height = Random.Shared.Next(800, 1081)
        },
        Locale     = "en-US",
        TimezoneId = "America/New_York"
    };

    private static async Task ApplyStealthAsync(IPage page)
        => await page.AddInitScriptAsync("Object.defineProperty(navigator, 'webdriver', { get: () => undefined });");
}
