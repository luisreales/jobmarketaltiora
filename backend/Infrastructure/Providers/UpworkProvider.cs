using System.Security.Cryptography;
using System.Text;
using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Scraping;

namespace backend.Infrastructure.Providers;

public sealed class UpworkProvider(
    IUpworkScraperClient scraperClient,
    IProviderSessionRepository sessionRepository) : IJobProvider
{
    public string Name => "upwork";

    public async Task<List<JobOffer>> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var isValid = await sessionRepository.IsAuthenticatedAsync(Name, ct);
        if (!isValid)
        {
            throw new InvalidOperationException(
                "Upwork session is expired or invalid. Re-authenticate with POST /api/auth/login using provider=upwork.");
        }

        try
        {
            var rawItems = await scraperClient.ScrapeAsync(request, ct);
            return rawItems.Select(Map).ToList();
        }
        catch (InvalidOperationException ex)
        {
            var message = ex.Message.ToLowerInvariant();
            if (message.Contains("session", StringComparison.Ordinal) ||
                message.Contains("login", StringComparison.Ordinal) ||
                message.Contains("blocked", StringComparison.Ordinal) ||
                message.Contains("challenge", StringComparison.Ordinal))
            {
                await sessionRepository.ClearSessionAsync(Name, ct);
                throw new InvalidOperationException(
                    "Upwork session expired/challenged. Manual login required via POST /api/auth/login with provider=upwork.", ex);
            }

            throw;
        }
    }

    public Task<bool> IsAuthenticatedAsync(CancellationToken ct)
    {
        return sessionRepository.IsAuthenticatedAsync(Name, ct);
    }

    private static JobOffer Map(RawJobData raw)
    {
        return new JobOffer
        {
            ExternalId = ComputeShortHash(raw.ExternalKey),
            Title = raw.Title,
            Company = string.IsNullOrWhiteSpace(raw.Company) ? "Client" : raw.Company,
            Location = string.IsNullOrWhiteSpace(raw.Location) ? "Remote" : raw.Location,
            Description = raw.Description,
            Url = raw.Url,
            Contact = null,
            SalaryRange = null,
            PublishedAt = null,
            Seniority = null,
            ContractType = null,
            Source = "upwork",
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
