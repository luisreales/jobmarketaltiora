namespace backend.Domain.Entities;

public class HotelPrice
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string Source { get; set; } = "unknown";
    public bool IsSeed { get; set; }
    public string? SearchCity { get; set; }
    public DateOnly? CheckIn { get; set; }
    public DateOnly? CheckOut { get; set; }
    public DateTime DateCaptured { get; set; } = DateTime.UtcNow;
    public Hotel? Hotel { get; set; }
}
