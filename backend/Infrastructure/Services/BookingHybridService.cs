using backend.Application.Interfaces;

namespace backend.Infrastructure.Services;

public class BookingHybridService(
    IBookingApiInterceptorService apiInterceptorService,
    IBookingPlaywrightService playwrightService,
    ILogger<BookingHybridService> logger) : IBookingHybridService
{
    public async Task<List<ScrapedHotelOffer>> SearchAsync(
        string location,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int kids,
        int rooms,
        CancellationToken cancellationToken = default)
    {
        var searchRequest = BookingUrlBuilder.BuildSearchRequest(location, checkIn, checkOut, adults, kids, rooms);
        var locationLabel = searchRequest.LocationLabel;

        logger.LogInformation(
            "Hybrid scraping started for {Location}. checkIn={CheckIn}, checkOut={CheckOut}, adults={Adults}, kids={Kids}, rooms={Rooms}, targetUrl={TargetUrl}",
            locationLabel,
            checkIn,
            checkOut,
            adults,
            kids,
            rooms,
            searchRequest.TargetUrl);

        List<ScrapedHotelOffer> apiResults = [];
        try
        {
            apiResults = await apiInterceptorService.SearchAsync(location, checkIn, checkOut, adults, kids, rooms, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "API interception strategy failed for {Location}", locationLabel);
        }

        if (apiResults.Count > 0)
        {
            logger.LogInformation("Hybrid scraper used API interception with {Count} results for {Location}", apiResults.Count, locationLabel);
            return apiResults;
        }

        logger.LogInformation("Hybrid scraper falling back to DOM strategy");
        try
        {
            var domResults = await playwrightService.SearchAsync(location, checkIn, checkOut, adults, kids, rooms, cancellationToken);
            if (domResults.Count > 0)
            {
                return domResults;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DOM scraping strategy failed for {Location}", locationLabel);
        }

        logger.LogWarning("Scraping unavailable for {Location}. Returning fallback offers.", locationLabel);
        return BuildFallbackOffers(locationLabel, checkIn, checkOut);
    }

    private static List<ScrapedHotelOffer> BuildFallbackOffers(string location, DateOnly checkIn, DateOnly checkOut)
    {
        var seed = Math.Abs(location.ToLowerInvariant().GetHashCode());
        var basePrice = 120m + (seed % 120);
        var now = DateTime.UtcNow;
        var fallbackUrl = BookingUrlBuilder.BuildSearchUrl(location, checkIn, checkOut, 2, 0, 1);

        return
        [
            new ScrapedHotelOffer($"{location} Central Hotel", location, basePrice, "USD", fallbackUrl, "fallback", now, checkIn, checkOut),
            new ScrapedHotelOffer($"{location} Riverside Suites", location, basePrice + 18m, "USD", fallbackUrl, "fallback", now, checkIn, checkOut),
            new ScrapedHotelOffer($"{location} Grand Palace", location, basePrice + 31m, "USD", fallbackUrl, "fallback", now, checkIn, checkOut),
            new ScrapedHotelOffer($"{location} Urban Stay", location, basePrice - 9m, "USD", fallbackUrl, "fallback", now, checkIn, checkOut)
        ];
    }
}
