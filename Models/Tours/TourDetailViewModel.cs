namespace ChillTour.Models.Tours;

public class TourDetailViewModel
{
    public long TourId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string TourCode { get; set; } = string.Empty;
    public string TourName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string StartDestinationName { get; set; } = string.Empty;
    public string EndDestinationName { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public int DurationNights { get; set; }
    public int TotalSeats { get; set; }
    public int RemainingSeats { get; set; }
    public long? TourScheduleId { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public decimal BasePrice { get; set; }
    public decimal? ChildPrice { get; set; }
    public decimal? SingleSupplement { get; set; }
    public bool IsLastMinuteDeal { get; set; }
    public string? DeparturePoint { get; set; }
    public string? ReturnPoint { get; set; }
    public bool PickupIncluded { get; set; }
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public List<string> ImageUrls { get; set; } = [];
    public List<TourDetailScheduleViewModel> Schedules { get; set; } = [];
    public List<TourDetailItineraryDayViewModel> ItineraryDays { get; set; } = [];
    public TourBookingFormViewModel BookingForm { get; set; } = new();
    public TourReviewFormViewModel ReviewForm { get; set; } = new();
    public List<TourReviewItemViewModel> Reviews { get; set; } = [];
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool CanReview { get; set; }
    public bool HasReviewed { get; set; }

    public bool IsSoldOut => RemainingSeats <= 0;
    public bool IsLowStock => !IsSoldOut && RemainingSeats <= 5;
    public bool CanBook => TourScheduleId.HasValue && !IsSoldOut;
}

public class TourDetailScheduleViewModel
{
    public long TourScheduleId { get; set; }
    public DateOnly DepartureDate { get; set; }
    public DateOnly ReturnDate { get; set; }
    public int AvailableSeats { get; set; }
    public int TotalSeats { get; set; }
    public decimal AdultPrice { get; set; }
    public decimal? ChildPrice { get; set; }
    public decimal? InfantPrice { get; set; }
    public decimal? SingleSupplement { get; set; }
    public byte Status { get; set; }
    public bool IsOpenForBooking => Status == 1 && AvailableSeats > 0;
}

public class TourDetailItineraryDayViewModel
{
    public int DayNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? OvernightStay { get; set; }
    public bool BreakfastIncluded { get; set; }
    public bool LunchIncluded { get; set; }
    public bool DinnerIncluded { get; set; }
    public string? HotelName { get; set; }
    public string? TransportationName { get; set; }
}

public class TourReviewFormViewModel
{
    public long TourId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class TourReviewItemViewModel
{
    public long ReviewId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public decimal Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> ImageUrls { get; set; } = [];
}
