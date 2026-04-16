using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Fakes;

internal static class TestApplicationDbContextFactory
{
    public static ApplicationDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }
}
