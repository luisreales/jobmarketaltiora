using backend.Application.Interfaces;

namespace backend.Infrastructure.Services;

public class DealAnalysisService(IHotelPriceRepository hotelPriceRepository) : IDealAnalysisService
{
    public async Task<(decimal currentPrice, decimal averagePrice, decimal score, bool isDeal)?> AnalyzeAsync(int hotelId, CancellationToken cancellationToken = default)
    {
        var current = await hotelPriceRepository.GetLatestByHotelIdAsync(hotelId, cancellationToken);
        var average = await hotelPriceRepository.GetAverageByHotelIdAsync(hotelId, cancellationToken);

        if (current is null || !average.HasValue || average.Value <= 0)
        {
            return null;
        }

        var score = (average.Value - current.Price) / average.Value;
        return (current.Price, average.Value, score, score > 0.10m);
    }
}
