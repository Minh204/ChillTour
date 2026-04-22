namespace ChillTour.Data.Entities;

public class Tour
{
    public long TourId { get; set; }
    public string TourCode { get; set; } = null!;
    public string TourName { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public int CategoryId { get; set; }
    public int StartDestinationId { get; set; }
    public int EndDestinationId { get; set; }
    public string? MainImageUrl { get; set; }
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public int DurationDays { get; set; }
    public int DurationNights { get; set; }
    public int MinGroupSize { get; set; }
    public int? MaxGroupSize { get; set; }
    public int TotalSeats { get; set; }
    public int RemainingSeats { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal BasePrice { get; set; }
    public decimal? ChildPrice { get; set; }
    public decimal? SingleSupplement { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public string? DeparturePoint { get; set; }
    public string? ReturnPoint { get; set; }
    public bool PickupIncluded { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsPublished { get; set; }
    public byte ApprovalStatus { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Category Category { get; set; } = null!;
    public Destination StartDestination { get; set; } = null!;
    public Destination EndDestination { get; set; } = null!;
    public ICollection<TourMedia> MediaItems { get; set; } = new List<TourMedia>();
    public ICollection<TourSchedule> Schedules { get; set; } = new List<TourSchedule>();
    public ICollection<TourItineraryDay> ItineraryDays { get; set; } = new List<TourItineraryDay>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
