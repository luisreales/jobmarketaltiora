using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Data;

public static class DatabaseSchemaUpdater
{
    public static async Task EnsureTraceabilityColumnsAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        var statements = new[]
        {
            "ALTER TABLE \"HotelPrices\" ADD COLUMN IF NOT EXISTS \"Source\" text NOT NULL DEFAULT 'unknown';",
            "ALTER TABLE \"HotelPrices\" ADD COLUMN IF NOT EXISTS \"IsSeed\" boolean NOT NULL DEFAULT false;",
            "ALTER TABLE \"HotelPrices\" ADD COLUMN IF NOT EXISTS \"SearchCity\" text NULL;",
            "ALTER TABLE \"HotelPrices\" ADD COLUMN IF NOT EXISTS \"CheckIn\" date NULL;",
            "ALTER TABLE \"HotelPrices\" ADD COLUMN IF NOT EXISTS \"CheckOut\" date NULL;"
        };

        foreach (var statement in statements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }
    }
}
