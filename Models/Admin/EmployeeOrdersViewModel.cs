namespace ChillTour.Models.Admin;

public class EmployeeOrdersViewModel
{
    public int PendingOrders { get; set; }
    public int PendingPaymentVerifications { get; set; }
    public int ConfirmedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public int PageSize { get; set; }
    public AdminOrdersFilterViewModel Filter { get; set; } = new();
    public IReadOnlyCollection<BookingItemViewModel> Bookings { get; set; } = Array.Empty<BookingItemViewModel>();
}
