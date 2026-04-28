using Microsoft.AspNetCore.Mvc.Rendering;

namespace ChillTour.Models.Admin;

public class AdminReportFilterViewModel
{
    public string Period { get; set; } = "month";
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public long? TourId { get; set; }
    public byte? BookingStatus { get; set; }
    public byte? PaymentStatus { get; set; }
    public byte? PaymentMethod { get; set; }
    public string? CustomerSearch { get; set; }
}

public class AdminReportPageViewModel
{
    public AdminReportFilterViewModel Filter { get; set; } = new();
    public AdminReportSummaryViewModel Summary { get; set; } = new();
    public IReadOnlyCollection<SelectListItem> PeriodOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyCollection<SelectListItem> TourOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyCollection<SelectListItem> BookingStatusOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyCollection<SelectListItem> PaymentStatusOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyCollection<SelectListItem> PaymentMethodOptions { get; set; } = Array.Empty<SelectListItem>();
    public bool CanSeeBusiness { get; set; }
    public bool CanSeeOperations { get; set; }
    public bool CanSeeFinance { get; set; }
    public bool CanSeeCustomer { get; set; }
    public bool CanSeeStaff { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
}

public class AdminReportSummaryViewModel
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TotalBookings { get; set; }
    public int PaidBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int TotalCustomers { get; set; }
    public int ActiveTours { get; set; }
    public int NewBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal WeekRevenue { get; set; }
    public decimal MonthRevenue { get; set; }
    public decimal CollectedRevenue { get; set; }
    public decimal PendingRevenue { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal AverageGuestsPerBooking { get; set; }
    public decimal RepeatBookingFrequency { get; set; }
    public int SuccessfulPayments { get; set; }
    public int FailedPayments { get; set; }
    public int ContractsSigned { get; set; }
    public int ContractsPending { get; set; }
    public decimal ContractSignSuccessRate { get; set; }
    public decimal AverageContractSigningHours { get; set; }
    public decimal CancellationRate { get; set; }
    public int NewCustomers { get; set; }
    public int ReturningCustomers { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int UpcomingSchedules { get; set; }
    public int LowSeatSchedules { get; set; }
    public int PendingStaffTasks { get; set; }
    public IReadOnlyCollection<AdminReportSeriesPointViewModel> RevenueSeries { get; set; } = Array.Empty<AdminReportSeriesPointViewModel>();
    public IReadOnlyCollection<AdminReportBreakdownItemViewModel> BookingStatusBreakdown { get; set; } = Array.Empty<AdminReportBreakdownItemViewModel>();
    public IReadOnlyCollection<AdminReportBreakdownItemViewModel> PaymentStatusBreakdown { get; set; } = Array.Empty<AdminReportBreakdownItemViewModel>();
    public IReadOnlyCollection<AdminReportBreakdownItemViewModel> PaymentMethodBreakdown { get; set; } = Array.Empty<AdminReportBreakdownItemViewModel>();
    public IReadOnlyCollection<AdminReportBreakdownItemViewModel> RevenueByDestination { get; set; } = Array.Empty<AdminReportBreakdownItemViewModel>();
    public IReadOnlyCollection<AdminReportBreakdownItemViewModel> PromotionBreakdown { get; set; } = Array.Empty<AdminReportBreakdownItemViewModel>();
    public IReadOnlyCollection<AdminReportTopTourViewModel> TopTours { get; set; } = Array.Empty<AdminReportTopTourViewModel>();
    public IReadOnlyCollection<AdminReportTopTourViewModel> LowBookingTours { get; set; } = Array.Empty<AdminReportTopTourViewModel>();
    public IReadOnlyCollection<AdminReportTopCustomerViewModel> TopCustomers { get; set; } = Array.Empty<AdminReportTopCustomerViewModel>();
    public IReadOnlyCollection<AdminReportScheduleViewModel> ScheduleSnapshot { get; set; } = Array.Empty<AdminReportScheduleViewModel>();
    public IReadOnlyCollection<AdminReportStaffPerformanceViewModel> StaffPerformance { get; set; } = Array.Empty<AdminReportStaffPerformanceViewModel>();
    public IReadOnlyCollection<AdminReportTransactionViewModel> RecentTransactions { get; set; } = Array.Empty<AdminReportTransactionViewModel>();
    public IReadOnlyCollection<AdminReportTaskItemViewModel> StaffTasks { get; set; } = Array.Empty<AdminReportTaskItemViewModel>();
    public IReadOnlyCollection<AdminReportAlertViewModel> Alerts { get; set; } = Array.Empty<AdminReportAlertViewModel>();
}

public class AdminReportSeriesPointViewModel
{
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int BookingCount { get; set; }
    public int Percent { get; set; }
}

public class AdminReportBreakdownItemViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
    public int Percent { get; set; }
}

public class AdminReportTopTourViewModel
{
    public long TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public string TourCode { get; set; } = string.Empty;
    public int BookingCount { get; set; }
    public decimal Revenue { get; set; }
    public decimal Rating { get; set; }
    public int CancelledBookings { get; set; }
    public decimal FillRate { get; set; }
}

public class AdminReportTopCustomerViewModel
{
    public long UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int BookingCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime LastBookingAt { get; set; }
}

public class AdminReportScheduleViewModel
{
    public string TourName { get; set; } = string.Empty;
    public DateOnly DepartureDate { get; set; }
    public int AvailableSeats { get; set; }
    public int ReservedSeats { get; set; }
    public int TotalSeats { get; set; }
    public decimal AdultPrice { get; set; }
}

public class AdminReportStaffPerformanceViewModel
{
    public string StaffName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int HandledActions { get; set; }
    public int ConfirmedBookings { get; set; }
    public int RefundCases { get; set; }
}

public class AdminReportTransactionViewModel
{
    public string PaymentCode { get; set; } = string.Empty;
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethodLabel { get; set; } = string.Empty;
    public string PaymentStatusLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AdminReportTaskItemViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Hint { get; set; } = string.Empty;
}

public class AdminReportAlertViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
}
