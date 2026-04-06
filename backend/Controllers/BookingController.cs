using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/booking")]
public class BookingController(
    IBookingHybridService bookingHybridService,
    IPriceTrackingService priceTrackingService,
    IHotelRepository hotelRepository,
    IHotelPriceRepository hotelPriceRepository) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<List<BookingSearchResponse>>> Search(
        [FromQuery] string? location,
        [FromQuery] string? city,
        [FromQuery] DateOnly checkIn,
        [FromQuery] DateOnly checkOut,
        CancellationToken cancellationToken,
        [FromQuery] int adults = 2,
        [FromQuery] int kids = 1,
        [FromQuery] int rooms = 1,
        [FromQuery] string mode = "real")
    {
        var targetLocation = string.IsNullOrWhiteSpace(location) ? city : location;

        if (string.IsNullOrWhiteSpace(targetLocation))
        {
            return BadRequest("location (country or city) is required");
        }

        var normalizedLocation = BookingUrlBuilder.ExtractLocationLabel(targetLocation);

        if (checkOut <= checkIn)
        {
            return BadRequest("checkOut must be greater than checkIn");
        }

        if (adults < 1 || kids < 0 || rooms < 1)
        {
            return BadRequest("invalid occupancy values. adults>=1, kids>=0, rooms>=1");
        }

        if (!string.Equals(mode, "real", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, "database", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("mode must be 'real' or 'database'");
        }

        if (string.Equals(mode, "database", StringComparison.OrdinalIgnoreCase))
        {
            var databaseResults = await SearchFromDatabaseAsync(normalizedLocation, checkIn, checkOut, cancellationToken);
            return Ok(databaseResults);
        }

        var offers = await bookingHybridService.SearchAsync(targetLocation, checkIn, checkOut, adults, kids, rooms, cancellationToken);

        var hasRealScrapedData = offers.Any(o =>
            !string.Equals(o.Source, "fallback", StringComparison.OrdinalIgnoreCase));

        if (!hasRealScrapedData)
        {
            var cachedResults = await SearchFromDatabaseAsync(normalizedLocation, checkIn, checkOut, cancellationToken, "database-scraped");
            if (cachedResults.Count > 0)
            {
                return Ok(cachedResults);
            }
        }

        try
        {
            await priceTrackingService.TrackAsync(offers, cancellationToken);
        }
        catch
        {
            // Search should still return results even if persistence fails.
        }

        return Ok(offers.Select(o => new BookingSearchResponse(
            o.Name,
            o.City,
            o.Price,
            o.Currency,
            o.OfferUrl,
            o.Source,
            o.CapturedAt,
            o.CheckIn,
            o.CheckOut,
                $"Live search captured at {o.CapturedAt:yyyy-MM-dd HH:mm} UTC using source '{o.Source}' (adults={adults}, kids={kids}, rooms={rooms}). URL={o.OfferUrl ?? "N/A"}")).ToList());
    }

    [HttpGet("discount-calendar")]
    public async Task<ActionResult<DiscountCalendarResponse>> DiscountCalendar(
        [FromQuery] string? location,
        [FromQuery] string? city,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken,
        [FromQuery] int nights = 2,
        [FromQuery] int adults = 2,
        [FromQuery] int kids = 0,
        [FromQuery] int rooms = 1)
    {
        var targetLocation = string.IsNullOrWhiteSpace(location) ? city : location;
        if (string.IsNullOrWhiteSpace(targetLocation))
        {
            return BadRequest("location (country, city, or booking URL) is required");
        }

        if (toDate < fromDate)
        {
            return BadRequest("toDate must be greater than or equal to fromDate");
        }

        var daysWindow = toDate.DayNumber - fromDate.DayNumber + 1;
        if (daysWindow > 21)
        {
            return BadRequest("date window too large. Maximum supported range is 21 days");
        }

        if (nights is < 1 or > 30)
        {
            return BadRequest("nights must be between 1 and 30");
        }

        if (adults < 1 || kids < 0 || rooms < 1)
        {
            return BadRequest("invalid occupancy values. adults>=1, kids>=0, rooms>=1");
        }

        var daily = new List<(DateOnly checkIn, DateOnly checkOut, decimal minPrice, string currency, string hotelName, string source)>();

        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            var checkOut = day.AddDays(nights);
            var offers = await bookingHybridService.SearchAsync(targetLocation, day, checkOut, adults, kids, rooms, cancellationToken);

            if (offers.Count == 0)
            {
                continue;
            }

            try
            {
                await priceTrackingService.TrackAsync(offers, cancellationToken);
            }
            catch
            {
                // Keep response flowing even if persistence fails for a specific day.
            }

            var bestOffer = offers.OrderBy(x => x.Price).First();
            daily.Add((day, checkOut, bestOffer.Price, bestOffer.Currency, bestOffer.Name, bestOffer.Source));
        }

        if (daily.Count == 0)
        {
            return NotFound("No offers found for the provided date window");
        }

        var average = daily.Average(x => x.minPrice);
        var threshold = 0.08m;

        var days = daily
            .OrderBy(x => x.checkIn)
            .Select(x =>
            {
                var score = average <= 0m ? 0m : Math.Round((average - x.minPrice) / average, 4);
                return new DiscountDayDto(
                    x.checkIn,
                    x.checkOut,
                    x.minPrice,
                    x.currency,
                    x.hotelName,
                    x.source,
                    score,
                    score >= threshold);
            })
            .ToList();

        var bestDay = daily.OrderBy(x => x.minPrice).First();

        return Ok(new DiscountCalendarResponse(
            targetLocation,
            fromDate,
            toDate,
            nights,
            bestDay.checkIn,
            bestDay.minPrice,
            bestDay.currency,
            days,
            "isDeal=true means the day is at least 8% cheaper than the average minimum price in the requested window."));
    }

    private async Task<List<BookingSearchResponse>> SearchFromDatabaseAsync(
        string city,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken,
        string databaseSourceLabel = "database")
    {
        var hotels = await hotelRepository.GetByCityAsync(city, cancellationToken);
        var results = new List<BookingSearchResponse>();

        foreach (var hotel in hotels)
        {
            var latestPrice = await hotelPriceRepository.GetLatestByHotelIdAsync(hotel.Id, cancellationToken);
            if (latestPrice is null)
            {
                continue;
            }

            results.Add(new BookingSearchResponse(
                hotel.Name,
                hotel.City,
                latestPrice.Price,
                latestPrice.Currency,
                null,
                databaseSourceLabel,
                latestPrice.DateCaptured,
                checkIn,
                checkOut,
                latestPrice.IsSeed
                    ? $"Seed sample data captured at {latestPrice.DateCaptured:yyyy-MM-dd HH:mm} UTC."
                    : $"Previously scraped ({latestPrice.Source}) at {latestPrice.DateCaptured:yyyy-MM-dd HH:mm} UTC."));
        }

        return results
            .OrderBy(r => r.Price)
            .Take(25)
            .ToList();
    }
}
