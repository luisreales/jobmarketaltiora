using backend.Domain.Entities;

namespace backend.Application.Interfaces;

public interface IProviderSessionRepository
{
    Task<ProviderSession> UpsertLoginAsync(
        string provider,
        string username,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default);
    Task<ProviderSession?> GetCurrentAsync(string provider, CancellationToken cancellationToken = default);
    Task<bool> IsAuthenticatedAsync(string provider, CancellationToken cancellationToken = default);
    Task MarkUsedAsync(string provider, CancellationToken cancellationToken = default);
    Task ClearSessionAsync(string provider, CancellationToken cancellationToken = default);
}
