using System.Security.Cryptography;
using System.Text;
using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Scraping;

namespace backend.Infrastructure.Providers;

public sealed class IndeedProvider(IPlaywrightScraper scraper) : IJobProvider
{
    public string Name => "indeed";

    public async Task<List<JobOffer>> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var rawItems = await scraper.ScrapeGoogleAsync(Name, request, ct);
        return rawItems.Select(Map).ToList();
    }

    public Task<bool> IsAuthenticatedAsync(CancellationToken ct)
    {
        return Task.FromResult(true);
    }

    private static JobOffer Map(RawJobData raw)
    {
        return new JobOffer
        {
            ExternalId = ComputeShortHash(raw.ExternalKey),
            Title = raw.Title,
            Company = string.IsNullOrWhiteSpace(raw.Company) ? "Unknown" : raw.Company,
            Location = raw.Location,
            Description = raw.Description,
            Url = raw.Url,
            Contact = null,
            SalaryRange = null,
            PublishedAt = null,
            Seniority = null,
            ContractType = null,
            Source = "indeed",
            SearchTerm = raw.SearchTerm,
            CapturedAt = raw.CapturedAt,
            MetadataJson = raw.MetadataJson
        };
    }

    private static string ComputeShortHash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..10].ToLowerInvariant();
    }
}