using backend.Domain.Entities;

namespace backend.Application.Interfaces;

public interface IJobRepository
{
    Task<List<JobOffer>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<JobOffer?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<JobOffer?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<int> UpsertRangeAsync(IEnumerable<JobOffer> jobs, CancellationToken cancellationToken = default);
}
