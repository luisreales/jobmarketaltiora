using System.Net.Http.Json;
using System.Text.Json;
using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Infrastructure.Scraping;

namespace backend.Infrastructure.Services;

public sealed class UpworkScraperClient(HttpClient httpClient, ILogger<UpworkScraperClient> logger) : IUpworkScraperClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record HealthResponse(string Status, bool Authenticated);

    public async Task<UpworkLoginResult> LoginAsync(string username, string password, bool showBrowser = false, CancellationToken cancellationToken = default)
    {
        await EnsureApiAvailableAsync(cancellationToken);

        var payload = new { username, password, showBrowser };
        using var response = await httpClient.PostAsJsonAsync("/upwork/login", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Upwork login API returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Upwork scraper login failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var result = await response.Content.ReadFromJsonAsync<UpworkLoginResult>(JsonOptions, cancellationToken);
        if (result is null)
        {
            throw new InvalidOperationException("Upwork scraper login response was empty.");
        }

        return result;
    }

    public async Task<List<RawJobData>> ScrapeAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureApiAvailableAsync(cancellationToken);

        using var response = await httpClient.PostAsJsonAsync("/upwork/scrape", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Upwork scrape API returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Upwork scraper scrape failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var result = await response.Content.ReadFromJsonAsync<UpworkScrapeResponse>(JsonOptions, cancellationToken);
        if (result?.Jobs is null)
        {
            throw new InvalidOperationException("Upwork scraper scrape response did not include jobs.");
        }

        return result.Jobs.Select(job => new RawJobData(
            ExternalKey: job.ExternalKey,
            Title: job.Title,
            Company: job.Company,
            Location: job.Location,
            Description: job.Description,
            Url: job.Url,
            Source: job.Source,
            SearchTerm: job.SearchTerm,
            CapturedAt: job.CapturedAt,
            MetadataJson: job.MetadataJson)).ToList();
    }

    private async Task EnsureApiAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync("/health", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("Upwork scraper API is not healthy. Please start the scraper service.");
            }

            var healthResult = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions, cancellationToken);
            if (healthResult?.Status != "ok")
            {
                throw new InvalidOperationException("Upwork scraper API health check failed.");
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to connect to Upwork scraper API. Make sure the scraper service is running.");
            throw new InvalidOperationException("Upwork scraper API is not available. Please start the scraper service with 'docker-compose up scraper-api'.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Timeout connecting to Upwork scraper API.");
            throw new InvalidOperationException("Timeout connecting to Upwork scraper API. The service may not be running.", ex);
        }
    }
}