using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

/// <summary>
/// Live integration smoke tests for AppSumo endpoints.
/// Requires the backend to be running (defaults to http://localhost:8081).
/// Run with: MARKET_API_BASE_URL=http://localhost:8081 dotnet test
/// </summary>
public class AppSumoEndpointsIntegrationTests
{
    private static HttpClient CreateClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable("MARKET_API_BASE_URL") ?? "http://localhost:8081";
        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout     = TimeSpan.FromSeconds(30)
        };
    }

    [Fact]
    public async Task Stats_ReturnsOkWithExpectedFields()
    {
        using var client = CreateClient();
        using var resp   = await client.GetAsync("/api/appsumo/stats");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(body).RootElement;

        Assert.True(doc.TryGetProperty("categories", out var cats),   "Missing 'categories' field");
        Assert.True(doc.TryGetProperty("products",   out var prods),  "Missing 'products' field");
        Assert.True(doc.TryGetProperty("reviews",    out var reviews), "Missing 'reviews' field");
        Assert.True(cats.GetInt32()   >= 0);
        Assert.True(prods.GetInt32()  >= 0);
        Assert.True(reviews.GetInt32() >= 0);
    }

    [Fact]
    public async Task Categories_ReturnsOkWithArrayPayload()
    {
        using var client = CreateClient();
        using var resp   = await client.GetAsync("/api/appsumo/categories");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task Products_ReturnsOkWithPagedShape()
    {
        using var client = CreateClient();
        using var resp   = await client.GetAsync("/api/appsumo/products?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(body).RootElement;

        Assert.True(doc.TryGetProperty("items",       out _), "Missing 'items'");
        Assert.True(doc.TryGetProperty("totalCount",  out _), "Missing 'totalCount'");
        Assert.True(doc.TryGetProperty("totalPages",  out _), "Missing 'totalPages'");
        Assert.True(doc.TryGetProperty("page",        out _), "Missing 'page'");
    }

    [Fact]
    public async Task Reviews_ReturnsOkWithPagedShape()
    {
        using var client = CreateClient();
        using var resp   = await client.GetAsync("/api/appsumo/reviews?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(body).RootElement;

        Assert.True(doc.TryGetProperty("items",      out _), "Missing 'items'");
        Assert.True(doc.TryGetProperty("totalCount", out _), "Missing 'totalCount'");
    }

    [Fact]
    public async Task Reviews_FilterByRating_OnlyReturnsLowRatingItems()
    {
        using var client = CreateClient();
        using var resp   = await client.GetAsync("/api/appsumo/reviews?tacoRating=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(body).RootElement;
        var items = doc.GetProperty("items");

        foreach (var item in items.EnumerateArray())
        {
            var rating = item.GetProperty("tacoRating").GetInt32();
            Assert.Equal(1, rating);
        }
    }

    [Fact]
    public async Task Runs_ReturnsOkWithArrayPayload()
    {
        using var client = CreateClient();
        using var resp   = await client.GetAsync("/api/appsumo/runs");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task StartScrape_DryRun_Returns202()
    {
        using var client = CreateClient();

        var payload = JsonSerializer.Serialize(new
        {
            startCategorySlug = (string?)null,
            dryRun            = true,
            maxProducts       = 2
        });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resp    = await client.PostAsync("/api/appsumo/scrape/start", content);

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Scrape started", body);
    }
}
