using backend.Application.Contracts;
using backend.Domain.Entities;

namespace backend.Application.Interfaces;

public interface IJobProvider
{
    string Name { get; }
    Task<List<JobOffer>> SearchAsync(SearchRequest request, CancellationToken ct);
    Task<bool> IsAuthenticatedAsync(CancellationToken ct);
}