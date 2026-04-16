using System.Net;
using System.Text.Json;
using Xunit;

namespace backend.IntegrationTests;

public class JobsEndpointsIntegrationTests
{
    [Fact]
    public async Task JobsQueryEndpoint_ShouldReturnPagedContract()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/jobs/jobs/query?page=1&pageSize=5");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.True(json.RootElement.TryGetProperty("page", out _));
        Assert.True(json.RootElement.TryGetProperty("pageSize", out _));
        Assert.True(json.RootElement.TryGetProperty("totalCount", out _));
    }

    [Fact]
    public async Task JobsFullEndpoint_ShouldReturnArray()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/jobs/jobs/full");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
    }

    [Fact]
    public async Task AuthStatusEndpoint_ShouldReturnProviderStatus()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/auth/status/linkedin");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("provider", out _));
        Assert.True(json.RootElement.TryGetProperty("isAuthenticated", out _));
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
