using backend.Domain.Entities;

namespace backend.Application.Interfaces;

public interface IHotelPriceRepository
{
    Task AddRangeAsync(IEnumerable<HotelPrice> prices, CancellationToken cancellationToken = default);
    Task<List<HotelPrice>> GetByHotelIdAsync(int hotelId, CancellationToken cancellationToken = default);
    Task<HotelPrice?> GetLatestByHotelIdAsync(int hotelId, CancellationToken cancellationToken = default);
    Task<HotelPrice?> GetLatestByHotelStayAsync(int hotelId, DateOnly? checkIn, DateOnly? checkOut, CancellationToken cancellationToken = default);
    Task<decimal?> GetAverageByHotelIdAsync(int hotelId, CancellationToken cancellationToken = default);
}
