using System.Net;
using System.Text.Json;
using Xunit;

namespace backend.IntegrationTests;

public class MarketEndpointsIntegrationTests
{
    [Fact]
    public async Task OpportunitiesEndpoint_ShouldReturnPagedContract()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/market/opportunities?page=1&pageSize=5");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("items", out var items));
        Assert.True(items.ValueKind == JsonValueKind.Array);
        Assert.True(json.RootElement.TryGetProperty("page", out _));
        Assert.True(json.RootElement.TryGetProperty("pageSize", out _));
    }

    [Fact]
    public async Task LeadsEndpoint_ShouldReturnPagedContract()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/market/leads?page=1&pageSize=5");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("items", out var items));
        Assert.True(items.ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public async Task TrendsEndpoint_ShouldReturnArrayContract()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/market/trends?windowDays=14");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);

        if (json.RootElement.GetArrayLength() > 0)
        {
            var first = json.RootElement[0];
            Assert.True(first.TryGetProperty("painCategory", out _));
            Assert.True(first.TryGetProperty("currentCount", out _));
            Assert.True(first.TryGetProperty("previousCount", out _));
            Assert.True(first.TryGetProperty("trendPercentage", out _));
        }
    }

    private static HttpClient CreateClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable("MARKET_API_BASE_URL") ?? "http://localhost:8080";
        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(20)
        };
    }
}
