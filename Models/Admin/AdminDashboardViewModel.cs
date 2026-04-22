namespace ChillTour.Models.Admin;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int NewUsersThisWeek { get; set; }
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int PaidBookings { get; set; }
    public int PublishedTours { get; set; }
    public int UpcomingSchedules { get; set; }
    public int LowSeatSchedules { get; set; }
    public decimal DepositRevenue { get; set; }
    public decimal ExpectedRevenue { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public decimal ConfirmationRate { get; set; }
    public List<AdminDashboardDailyMetricViewModel> DailyMetrics { get; set; } = [];
    public List<AdminDashboardRecentBookingViewModel> RecentBookings { get; set; } = [];
    public List<AdminDashboardTopTourViewModel> TopTours { get; set; } = [];
    public List<AdminDashboardUpcomingScheduleViewModel> UpcomingDepartures { get; set; } = [];
}

public class AdminDashboardDailyMetricViewModel
{
    public DateOnly Date { get; set; }
    public int BookingCount { get; set; }
    public int UserCount { get; set; }
    public decimal DepositRevenue { get; set; }
    public int BookingPercent { get; set; }
    public int UserPercent { get; set; }
}

public class AdminDashboardRecentBookingViewModel
{
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string TourName { get; set; } = string.Empty;
    public DateOnly DepartureDate { get; set; }
    public decimal TotalAmount { get; set; }
    public byte BookingStatus { get; set; }
    public byte PaymentStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminDashboardTopTourViewModel
{
    public string TourName { get; set; } = string.Empty;
    public int BookingCount { get; set; }
    public decimal Revenue { get; set; }
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }
}

public class AdminDashboardUpcomingScheduleViewModel
{
    public string TourName { get; set; } = string.Empty;
    public DateOnly DepartureDate { get; set; }
    public int AvailableSeats { get; set; }
    public int TotalSeats { get; set; }
    public decimal AdultPrice { get; set; }
}
