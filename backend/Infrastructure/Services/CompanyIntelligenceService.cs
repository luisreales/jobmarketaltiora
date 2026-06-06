using System.Text.Json;
using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Services;

public class CompanyIntelligenceService(
    ApplicationDbContext db,
    ILogger<CompanyIntelligenceService> logger) : ICompanyIntelligenceService
{
    private static readonly HashSet<string> AiTokens = new(StringComparer.Ordinal)
    {
        "OPENAI", "LANGCHAIN", "RAG", "VECTORDB", "AIAGENT", "PYTORCH",
        "TENSORFLOW", "LLAMA", "HUGGINGFACE", "SEMANTICKERNEL", "AUTOGEN", "CLAUDE", "MLFLOW"
    };

    private static readonly HashSet<string> CloudTokens = new(StringComparer.Ordinal)
    {
        "AZURE", "AWS", "GCP"
    };

    private static readonly HashSet<string> LegacyTokens = new(StringComparer.Ordinal)
    {
        "NET", "JAVA", "CSHARP", "SPRING", "EF"
    };

    public async Task<CompanyRebuildResultDto> RebuildAsync(CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        logger.LogInformation("CompanyIntelligenceService: rebuild started.");

        var jobs = await db.JobOffers
            .AsNoTracking()
            .Where(j => !string.IsNullOrWhiteSpace(j.Company))
            .Select(j => new
            {
                j.Id,
                j.Company,
                j.CompanyType,
                j.CapturedAt,
                Insight = db.JobInsights
                    .Where(i => i.JobId == j.Id)
                    .Select(i => new
                    {
                        i.Industry,
                        i.TechTokensJson,
                        i.PainCategory,
                        i.UrgencyScore,
                        i.OpportunityScore,
                        i.LeadScore,
                        i.IsDirectClient
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        logger.LogInformation("CompanyIntelligenceService: loaded {Count} jobs.", jobs.Count);

        var now = DateTime.UtcNow;
        var existing = await db.CompanyProfiles.ToDictionaryAsync(c => c.NormalizedName, c => c, ct);
        var upserted = 0;

        var byCompany = jobs.GroupBy(j => j.Company.Trim().ToLowerInvariant());

        foreach (var group in byCompany)
        {
            var items = group.ToList();
            var insighted = items.Where(j => j.Insight != null).ToList();

            var normalizedName = group.Key;
            var companyName = items.First().Company.Trim();

            var companyType = insighted.Count > 0
                ? insighted.GroupBy(j => j.CompanyType).MaxBy(g => g.Count())!.Key
                : items.First().CompanyType;

            var industry = insighted.Count > 0
                ? insighted.GroupBy(j => j.Insight!.Industry).MaxBy(g => g.Count())!.Key
                : "Unknown";

            var allTokens = insighted
                .SelectMany(j =>
                {
                    try { return JsonSerializer.Deserialize<string[]>(j.Insight!.TechTokensJson) ?? []; }
                    catch { return []; }
                })
                .Distinct()
                .ToList();

            var topPain = insighted.Count > 0
                ? insighted.GroupBy(j => j.Insight!.PainCategory).MaxBy(g => g.Count())!.Key
                : string.Empty;

            var avgUrgency = insighted.Count > 0 ? insighted.Average(j => j.Insight!.UrgencyScore) : 0;
            var avgOpp = insighted.Count > 0 ? insighted.Average(j => j.Insight!.OpportunityScore) : 0;
            var avgLead = insighted.Count > 0 ? insighted.Average(j => (double)j.Insight!.LeadScore) : 0;

            var firstSeen = items.Min(j => j.CapturedAt);
            var lastSeen = items.Max(j => j.CapturedAt);
            var weekSpan = Math.Max(1, (lastSeen - firstSeen).TotalDays / 7.0);
            var hiringVelocity = items.Count / weekSpan;

            var directCount = insighted.Count(j => j.Insight!.IsDirectClient);
            var isDirectClient = insighted.Count > 0 && directCount > insighted.Count / 2.0;

            var hasAi = allTokens.Any(t => AiTokens.Contains(t));
            var hasCloud = allTokens.Any(t => CloudTokens.Contains(t));
            var hasCloudMigration = hasCloud && allTokens.Any(t => LegacyTokens.Contains(t));

            var prospectScore = Math.Round(
                avgLead * 0.4 + avgUrgency * 10.0 * 0.3 + (isDirectClient ? 30.0 : 0.0),
                2);
            prospectScore = Math.Min(100, prospectScore);

            var techStackJson = JsonSerializer.Serialize(allTokens);

            if (existing.TryGetValue(normalizedName, out var ep))
            {
                ep.CompanyName = companyName;
                ep.CompanyType = companyType;
                ep.PrimaryIndustry = industry;
                ep.TechStackJson = techStackJson;
                ep.TopPainCategory = topPain;
                ep.TotalJobCount = items.Count;
                ep.AvgUrgencyScore = Math.Round(avgUrgency, 2);
                ep.AvgOpportunityScore = Math.Round(avgOpp, 2);
                ep.AvgLeadScore = Math.Round(avgLead, 2);
                ep.HiringVelocity = Math.Round(hiringVelocity, 2);
                ep.IsDirectClient = isDirectClient;
                ep.HasAiInitiative = hasAi;
                ep.HasCloudMigration = hasCloudMigration;
                ep.ProspectScore = prospectScore;
                ep.LastSeenAt = lastSeen;
                ep.UpdatedAt = now;
            }
            else
            {
                db.CompanyProfiles.Add(new CompanyProfile
                {
                    CompanyName = companyName,
                    NormalizedName = normalizedName,
                    CompanyType = companyType,
                    PrimaryIndustry = industry,
                    TechStackJson = techStackJson,
                    TopPainCategory = topPain,
                    TotalJobCount = items.Count,
                    AvgUrgencyScore = Math.Round(avgUrgency, 2),
                    AvgOpportunityScore = Math.Round(avgOpp, 2),
                    AvgLeadScore = Math.Round(avgLead, 2),
                    HiringVelocity = Math.Round(hiringVelocity, 2),
                    IsDirectClient = isDirectClient,
                    HasAiInitiative = hasAi,
                    HasCloudMigration = hasCloudMigration,
                    ProspectScore = prospectScore,
                    FirstSeenAt = firstSeen,
                    LastSeenAt = lastSeen,
                    UpdatedAt = now
                });
            }
            upserted++;
        }

        await db.SaveChangesAsync(ct);

        var duration = DateTime.UtcNow - startedAt;
        logger.LogInformation("CompanyIntelligenceService: done. companies={C} jobs={J} duration={D}",
            upserted, jobs.Count, duration);

        return new CompanyRebuildResultDto(upserted, jobs.Count, duration, now);
    }
}
