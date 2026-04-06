namespace backend.Application.Contracts;

public record HotelCreateRequest(string Name, string City);

public record HotelDto(int Id, string Name, string City, decimal? CurrentPrice, string CurrentPriceSource);

public record HotelPriceDto(
    decimal Price,
    string Currency,
    DateTime DateCaptured,
    string Source,
    bool IsSeed,
    string? SearchCity,
    DateOnly? CheckIn,
    DateOnly? CheckOut);

public record MonthlyPriceTrendDto(
    string Month,
    decimal AveragePrice,
    decimal MinPrice,
    decimal MaxPrice,
    int Samples);

public record HotelAnalysisDto(
    int HotelId,
    string HotelName,
    decimal CurrentPrice,
    decimal AveragePrice,
    decimal Score,
    bool IsDeal);

public record BookingSearchResponse(
    string Name,
    string City,
    decimal Price,
    string Currency,
    string? OfferUrl,
    string Source,
    DateTime CapturedAt,
    DateOnly CheckIn,
    DateOnly CheckOut,
    string? Note = null);

public record DiscountDayDto(
    DateOnly CheckIn,
    DateOnly CheckOut,
    decimal MinPrice,
    string Currency,
    string HotelName,
    string Source,
    decimal Score,
    bool IsDeal);

public record DiscountCalendarResponse(
    string Location,
    DateOnly FromDate,
    DateOnly ToDate,
    int Nights,
    DateOnly? BestCheckIn,
    decimal? BestPrice,
    string Currency,
    List<DiscountDayDto> Days,
    string Note);
