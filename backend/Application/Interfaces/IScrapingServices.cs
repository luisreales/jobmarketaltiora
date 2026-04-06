namespace backend.Application.Interfaces;

public record ScrapedHotelOffer(
    string Name,
    string City,
    decimal Price,
    string Currency,
    string? OfferUrl,
    string Source,
    DateTime CapturedAt,
    DateOnly CheckIn,
    DateOnly CheckOut);

public interface IBookingPlaywrightService
{
    Task<List<ScrapedHotelOffer>> SearchAsync(
        string location,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int kids,
        int rooms,
        CancellationToken cancellationToken = default);
}

public interface IBookingApiInterceptorService
{
    Task<List<ScrapedHotelOffer>> SearchAsync(
        string location,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int kids,
        int rooms,
        CancellationToken cancellationToken = default);
}

public interface IBookingHybridService
{
    Task<List<ScrapedHotelOffer>> SearchAsync(
        string location,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int kids,
        int rooms,
        CancellationToken cancellationToken = default);
}

public interface IPriceTrackingService
{
    Task TrackAsync(IEnumerable<ScrapedHotelOffer> offers, CancellationToken cancellationToken = default);
}

public interface IDealAnalysisService
{
    Task<(decimal currentPrice, decimal averagePrice, decimal score, bool isDeal)?> AnalyzeAsync(int hotelId, CancellationToken cancellationToken = default);
}
