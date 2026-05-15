namespace ChillTour.Models.Account;

public class CustomerBookingsPageViewModel
{
    public string? SearchTerm { get; set; }
    public byte? BookingStatus { get; set; }
    public byte? PaymentStatus { get; set; }
    public DateOnly? DepartureFrom { get; set; }
    public DateOnly? DepartureTo { get; set; }
    public string SortOrder { get; set; } = "newest";
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    public IReadOnlyList<CustomerBookingItemViewModel> Bookings { get; set; } = Array.Empty<CustomerBookingItemViewModel>();
}

public class CustomerInboxPageViewModel
{
    public string? SearchTerm { get; set; }
    public string? RelatedEntityType { get; set; }
    public bool? IsRead { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    public IReadOnlyList<CustomerNotificationItemViewModel> Notifications { get; set; } = Array.Empty<CustomerNotificationItemViewModel>();
}

public class CustomerWishlistPageViewModel
{
    public string? SearchTerm { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    public IReadOnlyList<CustomerWishlistItemViewModel> Tours { get; set; } = Array.Empty<CustomerWishlistItemViewModel>();
}

public class CustomerWishlistItemViewModel
{
    public long WishlistId { get; set; }
    public long TourId { get; set; }
    public string TourCode { get; set; } = string.Empty;
    public string TourName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal BasePrice { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public int RemainingSeats { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
