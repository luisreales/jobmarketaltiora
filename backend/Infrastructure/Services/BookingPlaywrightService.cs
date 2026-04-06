using System.Text.Json;
using System.Text.RegularExpressions;
using backend.Application.Interfaces;
using Microsoft.Playwright;

namespace backend.Infrastructure.Services;

public class BookingPlaywrightService(ILogger<BookingPlaywrightService> logger, IConfiguration configuration) : IBookingPlaywrightService
{
    private static readonly Regex TextPriceRegex = new(@"(?<currency>US\$|USD|COP|COL\$|COP\$|\$)\s*(?<amount>\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{1,2})?|\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NumberOnlyPriceRegex = new(@"(?<amount>\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{1,2})?|\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<List<ScrapedHotelOffer>> SearchAsync(
        string location,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int kids,
        int rooms,
        CancellationToken cancellationToken = default)
    {
        var headless = configuration.GetValue("Scraping:Headless", true);
        var domTimeoutMs = configuration.GetValue("Scraping:DomTimeoutMs", 10000);
        var minDelayMs = configuration.GetValue("Scraping:MinHumanDelayMs", 600);
        var maxDelayMs = configuration.GetValue("Scraping:MaxHumanDelayMs", 1600);
        var proxyServer = configuration.GetValue<string>("Scraping:Proxy:Server");
        var proxyUsername = configuration.GetValue<string>("Scraping:Proxy:Username");
        var proxyPassword = configuration.GetValue<string>("Scraping:Proxy:Password");
        var defaultCurrency = configuration.GetValue("Scraping:DefaultCurrency", "COP").ToUpperInvariant();
        var searchRequest = BookingUrlBuilder.BuildSearchRequest(location, checkIn, checkOut, adults, kids, rooms);
        var locationLabel = searchRequest.LocationLabel;

        logger.LogInformation(
            "DOM scraping started for {Location}. targetUrl={TargetUrl}, checkIn={CheckIn}, checkOut={CheckOut}, adults={Adults}, kids={Kids}, rooms={Rooms}, headless={Headless}",
            locationLabel,
            searchRequest.TargetUrl,
            checkIn,
            checkOut,
            adults,
            kids,
            rooms,
            headless);
        logger.LogInformation("DOM scraping network profile for {Location}. proxyEnabled={ProxyEnabled}", locationLabel, !string.IsNullOrWhiteSpace(proxyServer));

        try
        {
            using var playwright = await Playwright.CreateAsync();
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = headless,
                Args =
                [
                    "--disable-blink-features=AutomationControlled",
                    "--disable-dev-shm-usage",
                    "--no-sandbox"
                ]
            };

            if (!string.IsNullOrWhiteSpace(proxyServer))
            {
                launchOptions.Proxy = new Proxy
                {
                    Server = proxyServer,
                    Username = proxyUsername,
                    Password = proxyPassword
                };
            }

            await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
                Locale = "es-CO",
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    ["Accept-Language"] = "es-CO,es;q=0.9,en;q=0.8"
                }
            });

            var page = await context.NewPageAsync();
            await page.AddInitScriptAsync("Object.defineProperty(navigator, 'webdriver', { get: () => undefined });");

            await ApplyHumanDelayAsync(minDelayMs, maxDelayMs, cancellationToken);

            await page.GotoAsync(searchRequest.TargetUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 45000
            });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await ApplyHumanDelayAsync(minDelayMs, maxDelayMs, cancellationToken);

            var pageTitle = await page.TitleAsync();
            if (pageTitle.Contains("Security", StringComparison.OrdinalIgnoreCase)
                || pageTitle.Contains("challenge", StringComparison.OrdinalIgnoreCase)
                || page.Url.Contains("__challenge", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Booking challenge detected for {Location}. Skipping DOM scraping.", locationLabel);
                return [];
            }

            var selector = await WaitForAnySelectorAsync(
                page,
                [
                    "[data-testid='property-card']",
                    "[data-testid='availability-rate-information']",
                    "[data-testid='price-and-discounted-price']",
                    "[data-testid='title']"
                ],
                domTimeoutMs);

            if (selector is null)
            {
                logger.LogWarning("DOM scraping found no expected selectors for {Location}. Trying text extraction.", locationLabel);
                return await ExtractPricesFromPageTextAsync(page, locationLabel, checkIn, checkOut, searchRequest.IsDirectBookingUrl, defaultCurrency);
            }

            var json = await page.EvaluateAsync<string>(@"() => {
                const cardSelectors = ['[data-testid=""property-card""]', '[data-testid=""property-card-container""]'];
                let cards = [];

                for (const s of cardSelectors) {
                    cards = Array.from(document.querySelectorAll(s));
                    if (cards.length) break;
                }

                cards = cards.slice(0, 30);
                return JSON.stringify(cards.map(card => {
                    const name = card.querySelector('[data-testid=""title""]')?.textContent?.trim() || '';
                    const address = card.querySelector('[data-testid=""address-link""]')?.textContent?.trim() || '';
                    const linkEl = card.querySelector('[data-testid=""title-link""]') || card.querySelector('a[data-testid=""title-link""]') || card.querySelector('a[href*=""/hotel/""]') || card.querySelector('a[href*=""booking.com""]');
                    let offerUrl = '';
                    if (linkEl) {
                        const href = linkEl.getAttribute('href') || '';
                        if (href) {
                            try {
                                offerUrl = new URL(href, window.location.origin).href;
                            } catch {
                                offerUrl = href;
                            }
                        }
                    }
                    const rateInfo = card.querySelector('[data-testid=""availability-rate-information""]') || card;
                    const discounted = rateInfo.querySelector('[data-testid=""price-and-discounted-price""]')?.textContent || '';
                    const original = rateInfo.querySelector('.d68334ea31')?.textContent || '';
                    const genericPrice = rateInfo.querySelector('[data-testid=""price""]')?.textContent || '';
                    const hiddenPrice = Array.from(rateInfo.querySelectorAll('.bc946a29db'))
                      .map(x => (x.textContent || '').trim())
                      .find(x => /precio\s+(actual\s+)?(cop|usd|us\$|\$)/i.test(x) && !/impuestos|cargos|original/i.test(x)) || '';

                    const priceText = (discounted || hiddenPrice || genericPrice || original || '').replace(/\s+/g, ' ').trim();

                    return { name, address, priceText, offerUrl };
                }).filter(x => x.name && x.priceText));
            }");

            var parsed = JsonSerializer.Deserialize<List<DomHotelResult>>(json) ?? [];

            var offers = parsed
                .Select(p => new
                {
                    p.Name,
                    p.Address,
                    p.OfferUrl,
                    Parsed = ParsePriceToken(p.PriceText ?? string.Empty, defaultCurrency)
                })
                .Where(x => x.Parsed.HasValue)
                .Select(x => new ScrapedHotelOffer(
                    x.Name,
                    string.IsNullOrWhiteSpace(x.Address) ? locationLabel : x.Address,
                    x.Parsed!.Value.Price,
                    x.Parsed!.Value.Currency,
                    string.IsNullOrWhiteSpace(x.OfferUrl) ? searchRequest.TargetUrl : x.OfferUrl,
                    "dom",
                    DateTime.UtcNow,
                    checkIn,
                    checkOut))
                .OrderBy(o => o.Price)
                .Take(25)
                .ToList();

            if (offers.Count == 0)
            {
                logger.LogInformation("Card parsing returned no offers for {Location}. Trying text extraction.", locationLabel);
                return await ExtractPricesFromPageTextAsync(page, locationLabel, checkIn, checkOut, searchRequest.IsDirectBookingUrl, defaultCurrency);
            }

            logger.LogInformation("DOM scraping produced {Count} offers for {Location}", offers.Count, locationLabel);
            return offers;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DOM scraping failed for {Location}", locationLabel);
            return [];
        }
    }

    private static async Task<string?> WaitForAnySelectorAsync(IPage page, IEnumerable<string> selectors, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var selector in selectors)
        {
            var remaining = Math.Max(300, timeoutMs - (int)sw.ElapsedMilliseconds);
            if (remaining <= 0)
            {
                return null;
            }

            try
            {
                await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
                {
                    Timeout = remaining,
                    State = WaitForSelectorState.Attached
                });
                return selector;
            }
            catch
            {
            }
        }

        return null;
    }

    private async Task<List<ScrapedHotelOffer>> ExtractPricesFromPageTextAsync(
        IPage page,
        string location,
        DateOnly checkIn,
        DateOnly checkOut,
        bool isDirectBookingUrl,
        string defaultCurrency)
    {
        try
        {
            var pageText = await page.EvaluateAsync<string>("() => document.body?.innerText || ''");
            if (string.IsNullOrWhiteSpace(pageText))
            {
                return [];
            }

            var hotelName = await page.EvaluateAsync<string>("() => document.querySelector('[data-testid=\"title\"]')?.textContent?.trim() || document.querySelector('h1')?.textContent?.trim() || ''");
            if (string.IsNullOrWhiteSpace(hotelName))
            {
                hotelName = location;
            }

            var extractedMatches = TextPriceRegex.Matches(pageText)
                .Cast<Match>()
                .Select(m => new
                {
                    Currency = NormalizeCurrency(m.Groups["currency"].Value, defaultCurrency),
                    Price = ParseLocalizedAmount(m.Groups["amount"].Value),
                    IsLikelyRoomPrice = IsLikelyRoomPriceToken(pageText, m.Index, m.Length)
                })
                .Where(x => x.Price.HasValue && x.Price.Value >= 20m)
                .ToList();

            var preferredMatches = isDirectBookingUrl
                ? extractedMatches.Where(x => x.IsLikelyRoomPrice).ToList()
                : extractedMatches;

            if (isDirectBookingUrl && preferredMatches.Count < 3)
            {
                preferredMatches = extractedMatches;
            }

            var extracted = preferredMatches
                .Select(x => (x.Currency, x.Price!.Value))
                .Distinct()
                .OrderBy(x => x.Value)
                .Take(20)
                .ToList();

            if (extracted.Count == 0)
            {
                logger.LogWarning("Text extraction found no prices for {Location}", location);
                return [];
            }

            var now = DateTime.UtcNow;
            var offers = extracted
                .Select(item => new ScrapedHotelOffer(
                    hotelName!,
                    location,
                    item.Value,
                    item.Currency,
                    page.Url,
                    "dom-text",
                    now,
                    checkIn,
                    checkOut))
                .ToList();

            logger.LogInformation("Text extraction produced {Count} offers for {Location}", offers.Count, location);
            return offers;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Text extraction failed for {Location}", location);
            return [];
        }
    }

    private static decimal? ParseLocalizedAmount(string rawAmount)
    {
        var value = (rawAmount ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var hasComma = value.Contains(',');
        var hasDot = value.Contains('.');

        if (hasComma && hasDot)
        {
            var lastComma = value.LastIndexOf(',');
            var lastDot = value.LastIndexOf('.');
            var decimalSeparator = lastComma > lastDot ? ',' : '.';

            value = decimalSeparator == ','
                ? value.Replace(".", string.Empty).Replace(',', '.')
                : value.Replace(",", string.Empty);
        }
        else if (hasComma)
        {
            var commaCount = value.Count(c => c == ',');
            value = commaCount > 1 ? value.Replace(",", string.Empty) : value.Replace(',', '.');
        }
        else if (hasDot)
        {
            var dotCount = value.Count(c => c == '.');
            value = dotCount > 1 ? value.Replace(".", string.Empty) : value;
        }

        return decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static (decimal Price, string Currency)? ParsePriceToken(string rawPriceText, string defaultCurrency)
    {
        var matches = TextPriceRegex.Matches(rawPriceText ?? string.Empty)
            .Cast<Match>()
            .Select(m => new
            {
                Currency = NormalizeCurrency(m.Groups["currency"].Value, defaultCurrency),
                Price = ParseLocalizedAmount(m.Groups["amount"].Value)
            })
            .Where(x => x.Price.HasValue && x.Price.Value >= 20m)
            .OrderBy(x => x.Price)
            .ToList();

        if (matches.Count > 0)
        {
            var pick = matches.First();
            return (pick.Price!.Value, pick.Currency);
        }

        var numberMatches = NumberOnlyPriceRegex.Matches(rawPriceText ?? string.Empty)
            .Cast<Match>()
            .Select(m => ParseLocalizedAmount(m.Groups["amount"].Value))
            .Where(v => v.HasValue && v.Value >= 20m)
            .Select(v => v!.Value)
            .OrderBy(v => v)
            .ToList();

        return numberMatches.Count > 0
            ? (numberMatches.First(), defaultCurrency)
            : null;
    }

    private static string NormalizeCurrency(string rawCurrency, string defaultCurrency)
    {
        var code = (rawCurrency ?? string.Empty).Trim().ToUpperInvariant();
        return code switch
        {
            "US$" => "USD",
            "COL$" => "COP",
            "COP$" => "COP",
            "$" => defaultCurrency,
            "" => defaultCurrency,
            _ => code
        };
    }

    private static bool IsLikelyRoomPriceToken(string pageText, int index, int length)
    {
        var start = Math.Max(0, index - 42);
        var end = Math.Min(pageText.Length, index + length + 42);
        var context = pageText[start..end].ToLowerInvariant();

        if (context.Contains("impuestos")
            || context.Contains("tax")
            || context.Contains("cargo")
            || context.Contains("fees")
            || context.Contains("precio original")
            || context.Contains("precio tachado")
            || context.Contains("precio anterior"))
        {
            return false;
        }

        return context.Contains("precio")
            || context.Contains("precio actual")
            || context.Contains("price")
            || context.Contains("ver disponibilidad")
            || context.Contains("select rooms")
            || context.Contains("habitaci");
    }

    private static Task ApplyHumanDelayAsync(int minDelayMs, int maxDelayMs, CancellationToken cancellationToken)
    {
        var min = Math.Max(0, minDelayMs);
        var max = Math.Max(min, maxDelayMs);
        var delay = Random.Shared.Next(min, max + 1);
        return Task.Delay(delay, cancellationToken);
    }

    private sealed record DomHotelResult(string Name, string? Address, string? PriceText, string? OfferUrl);
}
