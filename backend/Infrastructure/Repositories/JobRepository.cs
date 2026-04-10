using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using backend.Application.Contracts;
using System.Text.RegularExpressions;

namespace backend.Infrastructure.Repositories;

public class JobRepository(ApplicationDbContext dbContext) : IJobRepository
{
    public async Task<List<JobOffer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.JobOffers
            .AsNoTracking()
            .OrderByDescending(x => x.CapturedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<JobOffer> Items, int TotalCount)> QueryAsync(
        JobsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 20);

        IQueryable<JobOffer> query = dbContext.JobOffers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Title, $"%{search}%") ||
                EF.Functions.ILike(x.Company, $"%{search}%") ||
                EF.Functions.ILike(x.Location, $"%{search}%") ||
                EF.Functions.ILike(x.Source, $"%{search}%") ||
                EF.Functions.ILike(x.SearchTerm, $"%{search}%") ||
                EF.Functions.ILike(x.SalaryRange ?? string.Empty, $"%{search}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            var title = request.Title.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Title, $"%{title}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.Company))
        {
            var company = request.Company.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Company, $"%{company}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            var location = request.Location.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Location, $"%{location}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            var source = request.Source.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Source, $"%{source}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim();
            query = query.Where(x => EF.Functions.ILike(x.SearchTerm, $"%{searchTerm}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.SalaryRange))
        {
            var salaryRange = request.SalaryRange.Trim();
            query = query.Where(x => EF.Functions.ILike(x.SalaryRange ?? string.Empty, $"%{salaryRange}%"));
        }

        query = ApplySorting(query, request.SortBy, request.SortDirection);

        var requireSalaryFilter = request.MinSalary.HasValue || request.MaxSalary.HasValue;
        if (!requireSalaryFilter)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            var pagedItems = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (pagedItems, totalCount);
        }

        var minSalary = request.MinSalary ?? 0;
        var maxSalary = request.MaxSalary ?? int.MaxValue;
        var materialized = await query.ToListAsync(cancellationToken);

        var filteredBySalary = materialized
            .Where(job =>
            {
                if (!TryParseSalaryRange(job.SalaryRange, out var range))
                {
                    return false;
                }

                var (rangeMin, rangeMax) = range;
                return rangeMax >= minSalary && rangeMin <= maxSalary;
            })
            .ToList();

        var totalAfterSalary = filteredBySalary.Count;
        var items = filteredBySalary
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalAfterSalary);
    }

    public async Task<JobOffer?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.JobOffers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<JobOffer?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await dbContext.JobOffers
            .FirstOrDefaultAsync(x => x.ExternalId == externalId, cancellationToken);
    }

    public async Task<int> UpsertRangeAsync(IEnumerable<JobOffer> jobs, CancellationToken cancellationToken = default)
    {
        var savedCount = 0;

        foreach (var job in jobs)
        {
            var existing = await dbContext.JobOffers
                .FirstOrDefaultAsync(x => x.ExternalId == job.ExternalId && x.Source == job.Source, cancellationToken);

            if (existing is null)
            {
                job.Category = "Unknown";
                job.OpportunityScore = 0;
                job.IsConsultingCompany = false;
                job.CompanyType = "Unknown";
                job.IsProcessed = false;
                job.ProcessedAt = null;
                dbContext.JobOffers.Add(job);
                savedCount++;
                continue;
            }

            existing.Title = job.Title;
            existing.Company = job.Company;
            existing.Location = job.Location;
            existing.Description = job.Description;
            existing.Url = job.Url;
            existing.Contact = job.Contact;
            existing.SalaryRange = job.SalaryRange;
            existing.PublishedAt = job.PublishedAt;
            existing.Seniority = job.Seniority;
            existing.ContractType = job.ContractType;
            existing.Source = job.Source;
            existing.SearchTerm = job.SearchTerm;
            existing.CapturedAt = job.CapturedAt;
            existing.MetadataJson = job.MetadataJson;
            existing.Category = "Unknown";
            existing.OpportunityScore = 0;
            existing.IsConsultingCompany = false;
            existing.CompanyType = "Unknown";
            existing.IsProcessed = false;
            existing.ProcessedAt = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return savedCount;
    }

    private static IQueryable<JobOffer> ApplySorting(
        IQueryable<JobOffer> query,
        string? sortBy,
        string? sortDirection)
    {
        var normalizedSortBy = (sortBy ?? "capturedAt").Trim().ToLowerInvariant();
        var isDescending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return normalizedSortBy switch
        {
            "title" => isDescending ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
            "company" => isDescending ? query.OrderByDescending(x => x.Company) : query.OrderBy(x => x.Company),
            "location" => isDescending ? query.OrderByDescending(x => x.Location) : query.OrderBy(x => x.Location),
            "source" => isDescending ? query.OrderByDescending(x => x.Source) : query.OrderBy(x => x.Source),
            "salaryrange" => isDescending ? query.OrderByDescending(x => x.SalaryRange) : query.OrderBy(x => x.SalaryRange),
            _ => isDescending ? query.OrderByDescending(x => x.CapturedAt) : query.OrderBy(x => x.CapturedAt)
        };
    }

    private static bool TryParseSalaryRange(string? salaryRange, out (int Min, int Max) range)
    {
        if (string.IsNullOrWhiteSpace(salaryRange))
        {
            range = default;
            return false;
        }

        var matches = Regex.Matches(salaryRange, @"\d+[\d,.]*")
            .Select(m => m.Value)
            .Select(value => value.Replace(",", string.Empty).Replace(".", string.Empty))
            .Select(value => int.TryParse(value, out var parsed) ? parsed : 0)
            .Where(value => value > 0)
            .ToList();

        if (matches.Count == 0)
        {
            range = default;
            return false;
        }

        if (matches.Count == 1)
        {
            range = (matches[0], matches[0]);
            return true;
        }

        var min = Math.Min(matches[0], matches[1]);
        var max = Math.Max(matches[0], matches[1]);
        range = (min, max);
        return true;
    }
}
