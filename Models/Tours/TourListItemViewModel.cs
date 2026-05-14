namespace ChillTour.Models.Tours;

public class TourListItemViewModel
{
    public long TourId { get; set; }
    public string TourCode { get; set; } = string.Empty;
    public string TourName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public int DurationNights { get; set; }
    public decimal BasePrice { get; set; }
    public string? ShortDescription { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsFeatured { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsLastMinute { get; set; }
    public int RemainingSeats { get; set; }
    public bool IsWishlisted { get; set; }
}
