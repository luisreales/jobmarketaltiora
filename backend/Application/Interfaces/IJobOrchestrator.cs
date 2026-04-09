using backend.Application.Contracts;
using backend.Domain.Entities;

namespace backend.Application.Interfaces;

public interface IJobOrchestrator
{
    Task LoginAsync(string provider, string username, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync(string provider, CancellationToken cancellationToken = default);
    Task<(bool isAuthenticated, DateTime? lastLoginAt, DateTime? lastUsedAt, DateTime? expiresAt)> GetAuthStatusAsync(
        string provider,
        CancellationToken cancellationToken = default);
    Task<(int savedCount, int totalFound)> SearchAndSaveAsync(
        string query,
        string? location,
        int limit,
        IReadOnlyCollection<string>? providers = null,
        int? totalPaging = null,
        int? startPage = null,
        int? endPage = null,
        CancellationToken cancellationToken = default);
    Task<List<JobOffer>> GetJobsAsync(CancellationToken cancellationToken = default);
    Task<(List<JobOffer> Items, int TotalCount)> QueryJobsAsync(JobsQueryRequest request, CancellationToken cancellationToken = default);
    Task<List<JobOffer>> GetHighValueLeadsAsync(CancellationToken cancellationToken = default);
    Task<JobOffer?> GetJobByIdAsync(int id, CancellationToken cancellationToken = default);
}
