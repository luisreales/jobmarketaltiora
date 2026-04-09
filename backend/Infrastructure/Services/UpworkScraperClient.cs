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

    public async Task<UpworkLoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var payload = new { username, password };
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
}