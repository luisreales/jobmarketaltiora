using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Repositories;

public class HotelRepository(ApplicationDbContext dbContext) : IHotelRepository
{
    public async Task<List<Hotel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Hotels
            .AsNoTracking()
            .OrderBy(h => h.City)
            .ThenBy(h => h.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Hotel>> GetByCityAsync(string city, CancellationToken cancellationToken = default)
    {
        return await dbContext.Hotels
            .AsNoTracking()
            .Where(h => EF.Functions.ILike(h.City, city) || EF.Functions.ILike(h.City, $"%{city}%"))
            .OrderBy(h => h.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Hotel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Hotels
            .Include(h => h.Prices)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<Hotel?> GetByNameCityAsync(string name, string city, CancellationToken cancellationToken = default)
    {
        return await dbContext.Hotels
            .FirstOrDefaultAsync(
                h => h.Name.ToLower() == name.ToLower() && h.City.ToLower() == city.ToLower(),
                cancellationToken);
    }

    public async Task<Hotel> AddAsync(Hotel hotel, CancellationToken cancellationToken = default)
    {
        dbContext.Hotels.Add(hotel);
        await dbContext.SaveChangesAsync(cancellationToken);
        return hotel;
    }
}
