using Microsoft.Playwright;

namespace backend.Application.Interfaces;

public interface ILinkedInAuthService
{
    Task LoginAndPersistSessionAsync(string username, string password);
    Task<IBrowserContext> GetAuthenticatedContextAsync();
    Task<bool> IsSessionValidAsync();
}