namespace ChillTour.Models.Account;

public class CustomerBookingsPageViewModel
{
    public string? SearchTerm { get; set; }
    public byte? BookingStatus { get; set; }
    public byte? PaymentStatus { get; set; }
    public DateOnly? DepartureFrom { get; set; }
    public DateOnly? DepartureTo { get; set; }
    public IReadOnlyList<CustomerBookingItemViewModel> Bookings { get; set; } = Array.Empty<CustomerBookingItemViewModel>();
}

public class CustomerInboxPageViewModel
{
    public string? SearchTerm { get; set; }
    public string? RelatedEntityType { get; set; }
    public bool? IsRead { get; set; }
    public IReadOnlyList<CustomerNotificationItemViewModel> Notifications { get; set; } = Array.Empty<CustomerNotificationItemViewModel>();
}
