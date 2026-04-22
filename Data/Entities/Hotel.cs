namespace ChillTour.Data.Entities;

public class Hotel
{
    public long HotelId { get; set; }
    public string HotelCode { get; set; } = null!;
    public string HotelName { get; set; } = null!;
    public int DestinationId { get; set; }
    public decimal? StarRating { get; set; }
    public string? AddressLine { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Description { get; set; }
    public TimeOnly? CheckInTime { get; set; }
    public TimeOnly? CheckOutTime { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public Destination Destination { get; set; } = null!;
}
