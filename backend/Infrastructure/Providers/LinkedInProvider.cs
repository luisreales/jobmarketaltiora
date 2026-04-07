using System.Security.Cryptography;
using System.Text;
using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Scraping;
using backend.Infrastructure.Sessions;

namespace backend.Infrastructure.Providers;

public sealed class LinkedInProvider(
    IPlaywrightScraper scraper,
    ISessionManager sessionManager) : IJobProvider
{
    public string Name => "linkedin";

    public async Task<List<JobOffer>> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var isValid = await sessionManager.ValidateAsync(Name, ct);
        if (!isValid)
        {
            throw new InvalidOperationException(
                "LinkedIn session is expired or invalid. Re-authenticate with POST /api/auth/login and complete manual verification if prompted.");
        }

        var storagePath = sessionManager.GetStorageStatePath(Name);
        var rawItems = await scraper.ScrapeLinkedInAsync(request, storagePath, ct);
        return rawItems.Select(Map).ToList();
    }

    public Task<bool> IsAuthenticatedAsync(CancellationToken ct)
    {
        return sessionManager.ValidateAsync(Name, ct);
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
            Source = "linkedin",
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
