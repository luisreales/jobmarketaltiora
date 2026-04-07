using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return savedCount;
    }
}
