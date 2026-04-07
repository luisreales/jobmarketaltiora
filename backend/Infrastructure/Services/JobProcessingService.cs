using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace backend.Infrastructure.Services;

public sealed class JobProcessingService(
    ApplicationDbContext dbContext,
    ICompanyClassifier classifier,
    IOpportunityScorer scorer,
    IMemoryCache memoryCache,
    IConfiguration configuration,
    ILogger<JobProcessingService> logger) : IJobProcessingService
{
    public async Task<int> ProcessUnprocessedJobsAsync(CancellationToken ct, int? batchSize = null, bool processAll = false)
    {
        var resolvedBatchSize = Math.Clamp(batchSize ?? configuration.GetValue<int?>("Jobs:PostProcessing:BatchSize") ?? 100, 10, 500);
        var totalProcessed = 0;

        do
        {
            var processed = await ProcessBatchAsync(resolvedBatchSize, ct);
            totalProcessed += processed;
            if (!processAll || processed == 0)
            {
                break;
            }
        } while (!ct.IsCancellationRequested);

        return totalProcessed;
    }

    public async Task<List<JobOffer>> GetLeadsAsync(JobFilter filter, CancellationToken ct = default)
    {
        var query = dbContext.JobOffers.AsNoTracking().Where(x => x.IsProcessed);

        if (filter.DirectOnly)
        {
            query = query.Where(x => !x.IsConsultingCompany);
        }

        var minScore = filter.MinScore ?? 60;
        query = query.Where(x => x.OpportunityScore >= minScore);

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var category = filter.Category.Trim();
            query = query.Where(x => x.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(filter.CompanyType))
        {
            var companyType = filter.CompanyType.Trim();
            query = query.Where(x => x.CompanyType == companyType);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Title, $"%{search}%") ||
                EF.Functions.ILike(x.Description, $"%{search}%") ||
                EF.Functions.ILike(x.Company, $"%{search}%"));
        }

        return await query
            .OrderByDescending(x => x.OpportunityScore)
            .ThenByDescending(x => x.CapturedAt)
            .ToListAsync(ct);
    }

    public Task<List<JobOffer>> GetProcessedAsync(CancellationToken ct = default)
    {
        return dbContext.JobOffers
            .AsNoTracking()
            .Where(x => x.IsProcessed)
            .OrderByDescending(x => x.ProcessedAt)
            .ToListAsync(ct);
    }

    public Task<List<JobOffer>> GetUnprocessedAsync(CancellationToken ct = default)
    {
        return dbContext.JobOffers
            .AsNoTracking()
            .Where(x => !x.IsProcessed)
            .OrderByDescending(x => x.CapturedAt)
            .ToListAsync(ct);
    }

    private async Task<int> ProcessBatchAsync(int batchSize, CancellationToken ct)
    {
        var jobs = await dbContext.JobOffers
            .Where(x => !x.IsProcessed)
            .OrderBy(x => x.CapturedAt)
            .Take(batchSize)
            .ToListAsync(ct);

        if (jobs.Count == 0)
        {
            return 0;
        }

        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();

            var cacheKey = $"job-post-process:{job.ExternalId}";
            if (memoryCache.TryGetValue<ProcessedResult>(cacheKey, out var cached) && cached is not null)
            {
                job.Category = cached.Category;
                job.IsConsultingCompany = cached.IsConsultingCompany;
                job.CompanyType = cached.CompanyType;
                job.OpportunityScore = cached.OpportunityScore;
                job.IsProcessed = true;
                job.ProcessedAt = DateTime.UtcNow;
                continue;
            }

            var classification = classifier.Classify(job.Company, job.Description);
            job.IsConsultingCompany = classification.IsConsultingCompany;
            job.CompanyType = classification.CompanyType;
            job.Category = ResolveCategory(job.Title, job.Description);
            job.OpportunityScore = scorer.Score(job);
            job.IsProcessed = true;
            job.ProcessedAt = DateTime.UtcNow;

            memoryCache.Set(cacheKey, new ProcessedResult(
                job.Category,
                job.IsConsultingCompany,
                job.CompanyType,
                job.OpportunityScore), TimeSpan.FromMinutes(30));
        }

        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Post-processing updated {Count} jobs.", jobs.Count);
        return jobs.Count;
    }

    private static string ResolveCategory(string title, string description)
    {
        var text = $"{title} {description}".ToLowerInvariant();

        if (text.Contains("data", StringComparison.Ordinal))
        {
            return "Data";
        }

        if (text.Contains("fullstack", StringComparison.Ordinal) ||
            (text.Contains("frontend", StringComparison.Ordinal) && text.Contains("backend", StringComparison.Ordinal)))
        {
            return "Fullstack";
        }

        if (text.Contains("backend", StringComparison.Ordinal) ||
            text.Contains("api", StringComparison.Ordinal) ||
            text.Contains("microservices", StringComparison.Ordinal))
        {
            return "Backend";
        }

        return "Unknown";
    }

    private sealed record ProcessedResult(
        string Category,
        bool IsConsultingCompany,
        string CompanyType,
        int OpportunityScore);
}
