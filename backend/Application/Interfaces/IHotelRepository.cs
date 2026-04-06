using backend.Domain.Entities;

namespace backend.Application.Interfaces;

public interface IHotelRepository
{
    Task<List<Hotel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Hotel>> GetByCityAsync(string city, CancellationToken cancellationToken = default);
    Task<Hotel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Hotel?> GetByNameCityAsync(string name, string city, CancellationToken cancellationToken = default);
    Task<Hotel> AddAsync(Hotel hotel, CancellationToken cancellationToken = default);
}