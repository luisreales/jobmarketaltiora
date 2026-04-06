using backend.Application.Interfaces;
using backend.Domain.Entities;

namespace backend.Infrastructure.Services;

public class PriceTrackingService(
    IHotelRepository hotelRepository,
    IHotelPriceRepository hotelPriceRepository,
    ILogger<PriceTrackingService> logger) : IPriceTrackingService
{
    public async Task TrackAsync(IEnumerable<ScrapedHotelOffer> offers, CancellationToken cancellationToken = default)
    {
        var trackedPrices = new List<HotelPrice>();

        foreach (var offer in offers)
        {
            var hotel = await hotelRepository.GetByNameCityAsync(offer.Name, offer.City, cancellationToken)
                ?? await hotelRepository.AddAsync(new Hotel
                {
                    Name = offer.Name,
                    City = offer.City
                }, cancellationToken);

            var previous = await hotelPriceRepository.GetLatestByHotelStayAsync(hotel.Id, offer.CheckIn, offer.CheckOut, cancellationToken);

            if (previous is not null)
            {
                var delta = offer.Price - previous.Price;
                var pct = previous.Price == 0m ? 0m : Math.Round((delta / previous.Price) * 100m, 2);

                if (offer.Price < previous.Price)
                {
                    logger.LogInformation(
                        "Price drop detected: hotel={HotelName}, stay={CheckIn}->{CheckOut}, previous={PreviousPrice}, current={CurrentPrice}, delta={Delta}, pct={Pct}%",
                        hotel.Name,
                        offer.CheckIn,
                        offer.CheckOut,
                        previous.Price,
                        offer.Price,
                        delta,
                        pct);
                }
                else if (offer.Price > previous.Price)
                {
                    logger.LogInformation(
                        "Price increase detected: hotel={HotelName}, stay={CheckIn}->{CheckOut}, previous={PreviousPrice}, current={CurrentPrice}, delta={Delta}, pct={Pct}%",
                        hotel.Name,
                        offer.CheckIn,
                        offer.CheckOut,
                        previous.Price,
                        offer.Price,
                        delta,
                        pct);
                }
            }

            trackedPrices.Add(new HotelPrice
            {
                HotelId = hotel.Id,
                Price = offer.Price,
                Currency = offer.Currency,
                Source = offer.Source,
                IsSeed = false,
                SearchCity = offer.City,
                CheckIn = offer.CheckIn,
                CheckOut = offer.CheckOut,
                DateCaptured = offer.CapturedAt
            });
        }

        if (trackedPrices.Count == 0)
        {
            return;
        }

        await hotelPriceRepository.AddRangeAsync(trackedPrices, cancellationToken);
        logger.LogInformation("Stored {Count} price records", trackedPrices.Count);
    }
}
