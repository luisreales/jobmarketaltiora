namespace backend.Domain.Entities;

public class Hotel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public ICollection<HotelPrice> Prices { get; set; } = new List<HotelPrice>();
}
