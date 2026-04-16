using System.Net;
using System.Text.Json;
using Xunit;

namespace backend.IntegrationTests;

public class SemanticEndpointIntegrationTests
{
    [Fact]
    public async Task SemanticHelloEndpoint_ShouldReturnSuccessfulHealthPayload()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/semantic/hello?prompt=health-check");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("prompt", out var prompt));
        Assert.Equal("health-check", prompt.GetString());

        Assert.True(json.RootElement.TryGetProperty("response", out var modelResponse));
        Assert.True(modelResponse.ValueKind is JsonValueKind.String or JsonValueKind.Null);

        Assert.True(json.RootElement.TryGetProperty("hasResponse", out var hasResponse));
        var hasModelResponse = hasResponse.GetBoolean();

        if (hasModelResponse)
        {
            Assert.False(string.IsNullOrWhiteSpace(modelResponse.GetString()));
        }

        Assert.True(json.RootElement.TryGetProperty("modelUsed", out var modelUsed));
        Assert.False(string.IsNullOrWhiteSpace(modelUsed.GetString()));
    }

    [Fact]
    public async Task LlmHealthEndpoint_ShouldReturnHealthContract()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/ai/llm-health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("isConfigured", out var isConfigured));
        Assert.True(isConfigured.ValueKind == JsonValueKind.True || isConfigured.ValueKind == JsonValueKind.False);

        Assert.True(json.RootElement.TryGetProperty("isHealthy", out var isHealthy));
        Assert.True(isHealthy.ValueKind == JsonValueKind.True || isHealthy.ValueKind == JsonValueKind.False);

        Assert.True(json.RootElement.TryGetProperty("state", out var state));
        var stateValue = state.GetString();
        Assert.Contains(stateValue, new[] { "healthy", "degraded", "not-configured" });

        Assert.True(json.RootElement.TryGetProperty("modelId", out var modelId));
        Assert.True(modelId.ValueKind is JsonValueKind.String or JsonValueKind.Null);

        Assert.True(json.RootElement.TryGetProperty("lastSuccessAt", out _));
        Assert.True(json.RootElement.TryGetProperty("lastFailureAt", out _));

        Assert.True(json.RootElement.TryGetProperty("message", out var message));
        Assert.False(string.IsNullOrWhiteSpace(message.GetString()));
    }

    private static HttpClient CreateClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable("MARKET_API_BASE_URL") ?? "http://localhost:8080";
        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(60)
        };
    }
}