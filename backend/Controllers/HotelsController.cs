using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/hotels")]
public class HotelsController(
    IHotelRepository hotelRepository,
    IHotelPriceRepository hotelPriceRepository,
    IDealAnalysisService dealAnalysisService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<HotelDto>>> GetHotels(CancellationToken cancellationToken)
    {
        var hotels = await hotelRepository.GetAllAsync(cancellationToken);
        var results = new List<HotelDto>(hotels.Count);

        foreach (var hotel in hotels)
        {
            var currentPrice = await hotelPriceRepository.GetLatestByHotelIdAsync(hotel.Id, cancellationToken);
            results.Add(new HotelDto(
                hotel.Id,
                hotel.Name,
                hotel.City,
                currentPrice?.Price,
                currentPrice?.Source ?? "none"));
        }

        return Ok(results);
    }

    [HttpGet("{id:int}/prices")]
    public async Task<ActionResult<List<HotelPriceDto>>> GetHotelPrices(int id, CancellationToken cancellationToken)
    {
        var hotel = await hotelRepository.GetByIdAsync(id, cancellationToken);
        if (hotel is null)
        {
            return NotFound();
        }

        var prices = await hotelPriceRepository.GetByHotelIdAsync(id, cancellationToken);
        return Ok(prices.Select(p => new HotelPriceDto(
            p.Price,
            p.Currency,
            p.DateCaptured,
            p.Source,
            p.IsSeed,
            p.SearchCity,
            p.CheckIn,
            p.CheckOut)).ToList());
    }

    [HttpGet("{id:int}/prices/monthly")]
    public async Task<ActionResult<List<MonthlyPriceTrendDto>>> GetMonthlyPriceTrend(
        int id,
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default)
    {
        if (months is < 1 or > 36)
        {
            return BadRequest("months must be between 1 and 36");
        }

        var hotel = await hotelRepository.GetByIdAsync(id, cancellationToken);
        if (hotel is null)
        {
            return NotFound();
        }

        var prices = await hotelPriceRepository.GetByHotelIdAsync(id, cancellationToken);
        var cutoff = DateTime.UtcNow.AddMonths(-months);

        var trend = prices
            .Where(p => p.DateCaptured >= cutoff)
            .GroupBy(p => new { p.DateCaptured.Year, p.DateCaptured.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyPriceTrendDto(
                $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                Math.Round(g.Average(x => x.Price), 2),
                g.Min(x => x.Price),
                g.Max(x => x.Price),
                g.Count()))
            .ToList();

        return Ok(trend);
    }

    [HttpGet("{id:int}/analysis")]
    public async Task<ActionResult<HotelAnalysisDto>> GetAnalysis(int id, CancellationToken cancellationToken)
    {
        var hotel = await hotelRepository.GetByIdAsync(id, cancellationToken);
        if (hotel is null)
        {
            return NotFound();
        }

        var analysis = await dealAnalysisService.AnalyzeAsync(id, cancellationToken);
        if (analysis is null)
        {
            return NotFound("Not enough price history to calculate analysis");
        }

        return Ok(new HotelAnalysisDto(
            id,
            hotel.Name,
            analysis.Value.currentPrice,
            analysis.Value.averagePrice,
            analysis.Value.score,
            analysis.Value.isDeal));
    }

    [HttpPost]
    public async Task<ActionResult<HotelDto>> CreateHotel([FromBody] HotelCreateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.City))
        {
            return BadRequest("name and city are required");
        }

        var existing = await hotelRepository.GetByNameCityAsync(request.Name, request.City, cancellationToken);
        if (existing is not null)
        {
            return Conflict("Hotel already exists");
        }

        var created = await hotelRepository.AddAsync(new Hotel
        {
            Name = request.Name,
            City = request.City
        }, cancellationToken);

        return CreatedAtAction(
            nameof(GetHotels),
            new { id = created.Id },
            new HotelDto(created.Id, created.Name, created.City, null, "none"));
    }
}
