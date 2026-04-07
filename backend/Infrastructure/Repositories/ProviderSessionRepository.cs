using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Repositories;

public class ProviderSessionRepository(ApplicationDbContext dbContext) : IProviderSessionRepository
{
    public async Task<ProviderSession> UpsertLoginAsync(
        string provider,
        string username,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default)
    {
        var current = await dbContext.ProviderSessions
            .FirstOrDefaultAsync(x => x.Provider == provider.ToLowerInvariant(), cancellationToken);

        if (current is null)
        {
            current = new ProviderSession();
            dbContext.ProviderSessions.Add(current);
        }

        current.Provider = provider.ToLowerInvariant();
        current.Username = username;
        current.IsAuthenticated = true;
        current.LastLoginAt = DateTime.UtcNow;
        current.LastUsedAt = DateTime.UtcNow;
        current.ExpiresAt = expiresAt;

        await dbContext.SaveChangesAsync(cancellationToken);
        return current;
    }

    public async Task<ProviderSession?> GetCurrentAsync(string provider, CancellationToken cancellationToken = default)
    {
        return await dbContext.ProviderSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Provider == provider.ToLowerInvariant(), cancellationToken);
    }

    public async Task<bool> IsAuthenticatedAsync(string provider, CancellationToken cancellationToken = default)
    {
        var current = await dbContext.ProviderSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Provider == provider.ToLowerInvariant(), cancellationToken);

        if (current is null || !current.IsAuthenticated)
        {
            return false;
        }

        if (current.ExpiresAt.HasValue && current.ExpiresAt.Value <= DateTime.UtcNow)
        {
            return false;
        }

        return true;
    }

    public async Task MarkUsedAsync(string provider, CancellationToken cancellationToken = default)
    {
        var current = await dbContext.ProviderSessions
            .FirstOrDefaultAsync(x => x.Provider == provider.ToLowerInvariant(), cancellationToken);

        if (current is null)
        {
            return;
        }

        current.LastUsedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearSessionAsync(string provider, CancellationToken cancellationToken = default)
    {
        var current = await dbContext.ProviderSessions
            .FirstOrDefaultAsync(x => x.Provider == provider.ToLowerInvariant(), cancellationToken);

        if (current is null)
        {
            return;
        }

        current.IsAuthenticated = false;
        current.ExpiresAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
