using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Hotels.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;

        var hotels = new List<Hotel>
        {
            new() { Name = "Hotel Gran Via", City = "Madrid" },
            new() { Name = "Riverside Palace", City = "Lisbon" }
        };

        dbContext.Hotels.AddRange(hotels);
        await dbContext.SaveChangesAsync(cancellationToken);

        var priceHistory = new List<HotelPrice>
        {
            new()
            {
                HotelId = hotels[0].Id,
                Price = 189m,
                Currency = "USD",
                Source = "seed",
                IsSeed = true,
                SearchCity = "Madrid",
                CheckIn = DateOnly.FromDateTime(now.AddDays(3)),
                CheckOut = DateOnly.FromDateTime(now.AddDays(6)),
                DateCaptured = now.AddDays(-1)
            },
            new()
            {
                HotelId = hotels[1].Id,
                Price = 159m,
                Currency = "USD",
                Source = "seed",
                IsSeed = true,
                SearchCity = "Lisbon",
                CheckIn = DateOnly.FromDateTime(now.AddDays(3)),
                CheckOut = DateOnly.FromDateTime(now.AddDays(6)),
                DateCaptured = now.AddDays(-1)
            }
        };

        dbContext.HotelPrices.AddRange(priceHistory);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
