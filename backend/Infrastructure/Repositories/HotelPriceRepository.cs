using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Repositories;

public class HotelPriceRepository(ApplicationDbContext dbContext) : IHotelPriceRepository
{
    public async Task AddRangeAsync(IEnumerable<HotelPrice> prices, CancellationToken cancellationToken = default)
    {
        dbContext.HotelPrices.AddRange(prices);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<HotelPrice>> GetByHotelIdAsync(int hotelId, CancellationToken cancellationToken = default)
    {
        return await dbContext.HotelPrices
            .AsNoTracking()
            .Where(p => p.HotelId == hotelId)
            .OrderBy(p => p.DateCaptured)
            .ToListAsync(cancellationToken);
    }

    public async Task<HotelPrice?> GetLatestByHotelIdAsync(int hotelId, CancellationToken cancellationToken = default)
    {
        return await dbContext.HotelPrices
            .AsNoTracking()
            .Where(p => p.HotelId == hotelId)
            .OrderByDescending(p => p.DateCaptured)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<HotelPrice?> GetLatestByHotelStayAsync(int hotelId, DateOnly? checkIn, DateOnly? checkOut, CancellationToken cancellationToken = default)
    {
        return await dbContext.HotelPrices
            .AsNoTracking()
            .Where(p => p.HotelId == hotelId && p.CheckIn == checkIn && p.CheckOut == checkOut)
            .OrderByDescending(p => p.DateCaptured)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<decimal?> GetAverageByHotelIdAsync(int hotelId, CancellationToken cancellationToken = default)
    {
        return await dbContext.HotelPrices
            .AsNoTracking()
            .Where(p => p.HotelId == hotelId)
            .Select(p => (decimal?)p.Price)
            .AverageAsync(cancellationToken);
    }
}
