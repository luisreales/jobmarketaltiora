using System.Text.RegularExpressions;
using backend.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace backend.Infrastructure.Services;

public class BookingApiInterceptorService(
    ILogger<BookingApiInterceptorService> logger,
    IConfiguration configuration) : IBookingApiInterceptorService
{
    private static readonly Regex PriceRegex = new("\"price\"\\s*:\\s*(\\d+(?:\\.\\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CurrencyRegex = new("\"currency(?:_code)?\"\\s*:\\s*\"([A-Za-z$]{1,8})\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NameRegex = new("\"name\"\\s*:\\s*\"([^\"]{3,120})\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<List<ScrapedHotelOffer>> SearchAsync(
        string location,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int kids,
        int rooms,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ScrapedHotelOffer>();
        var minDelayMs = configuration.GetValue("Scraping:MinHumanDelayMs", 600);
        var maxDelayMs = configuration.GetValue("Scraping:MaxHumanDelayMs", 1600);
        var proxyServer = configuration.GetValue<string>("Scraping:Proxy:Server");
        var proxyUsername = configuration.GetValue<string>("Scraping:Proxy:Username");
        var proxyPassword = configuration.GetValue<string>("Scraping:Proxy:Password");
        var defaultCurrency = configuration.GetValue("Scraping:DefaultCurrency", "COP").ToUpperInvariant();
        var searchRequest = BookingUrlBuilder.BuildSearchRequest(location, checkIn, checkOut, adults, kids, rooms);
        var locationLabel = searchRequest.LocationLabel;

        logger.LogInformation(
            "API interception scraping started for {Location}. targetUrl={TargetUrl}, checkIn={CheckIn}, checkOut={CheckOut}",
            locationLabel,
            searchRequest.TargetUrl,
            checkIn,
            checkOut);

        try
        {
            using var playwright = await Playwright.CreateAsync();
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = true,
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

            logger.LogInformation("API interception network profile for {Location}. proxyEnabled={ProxyEnabled}", locationLabel, !string.IsNullOrWhiteSpace(proxyServer));
            await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
                Locale = "es-CO",
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    ["Accept-Language"] = "es-CO,es;q=0.9,en;q=0.8"
                }
            });

            var page = await context.NewPageAsync();
            await page.AddInitScriptAsync("Object.defineProperty(navigator, 'webdriver', { get: () => undefined });");
            await Task.Delay(Random.Shared.Next(Math.Max(0, minDelayMs), Math.Max(minDelayMs, maxDelayMs) + 1), cancellationToken);

            page.Response += async (_, response) =>
            {
                if (!response.Ok || !response.Url.Contains("booking", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var contentType = response.Headers.TryGetValue("content-type", out var value) ? value : string.Empty;
                if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                try
                {
                    var body = await response.TextAsync();
                    var prices = PriceRegex.Matches(body).Select(m => m.Groups[1].Value).Take(20).ToList();
                    var names = NameRegex.Matches(body).Select(m => m.Groups[1].Value).Take(20).ToList();
                    var currencyRaw = CurrencyRegex.Match(body).Success
                        ? CurrencyRegex.Match(body).Groups[1].Value
                        : defaultCurrency;
                    var parsedCurrency = NormalizeCurrency(currencyRaw, defaultCurrency);

                    var count = Math.Min(prices.Count, names.Count);
                    for (var i = 0; i < count; i++)
                    {
                        if (!decimal.TryParse(prices[i], out var parsedPrice))
                        {
                            continue;
                        }

                        results.Add(new ScrapedHotelOffer(
                            names[i],
                            locationLabel,
                            parsedPrice,
                            parsedCurrency,
                            searchRequest.TargetUrl,
                            "api",
                            DateTime.UtcNow,
                            checkIn,
                            checkOut));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Unable to parse intercepted API response from {Url}", response.Url);
                }
            };

            await page.GotoAsync(searchRequest.TargetUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 45000
            });

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForTimeoutAsync(3000);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "API interception scraping failed for {Location}", locationLabel);
            return [];
        }

        var normalizedResults = results
            .GroupBy(r => r.Name)
            .Select(g => g.First())
            .OrderBy(r => r.Price)
            .Take(25)
            .ToList();

        logger.LogInformation("API interception scraping finished for {Location}. offers={Count}", locationLabel, normalizedResults.Count);
        return normalizedResults;
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
}
