using backend.Application.Contracts;
using backend.Domain.Entities;

namespace backend.Application.Interfaces;

public interface IJobProcessingService
{
    Task<int> ProcessUnprocessedJobsAsync(CancellationToken ct, int? batchSize = null, bool processAll = false);
    Task<List<JobOffer>> GetLeadsAsync(JobFilter filter, CancellationToken ct = default);
    Task<List<JobOffer>> GetProcessedAsync(CancellationToken ct = default);
    Task<List<JobOffer>> GetUnprocessedAsync(CancellationToken ct = default);
}