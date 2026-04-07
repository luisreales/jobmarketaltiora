namespace backend.Infrastructure.Sessions;

public interface ISessionManager
{
    Task<bool> ValidateAsync(string provider, CancellationToken ct = default);
    Task SaveAsync(string provider, object sessionData, CancellationToken ct = default);
    Task ClearAsync(string provider, CancellationToken ct = default);
    Task LoginAsync(string provider, string username, string password, CancellationToken ct = default);
    string GetStorageStatePath(string provider);
}
