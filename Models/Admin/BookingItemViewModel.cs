namespace ChillTour.Models.Admin;

public class BookingItemViewModel
{
    public long BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string TourName { get; set; } = string.Empty;
    public string TourCode { get; set; } = string.Empty;
    public DateOnly DepartureDate { get; set; }
    public DateOnly ReturnDate { get; set; }
    public int AdultCount { get; set; }
    public int ChildCount { get; set; }
    public int InfantCount { get; set; }
    public int Travelers { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public byte BookingStatus { get; set; }
    public byte PaymentStatus { get; set; }
    public DateTime? BalanceDueAt { get; set; }
    public long? LatestPaymentId { get; set; }
    public string LatestPaymentCode { get; set; } = string.Empty;
    public decimal LatestPaymentAmount { get; set; }
    public byte? LatestPaymentStatus { get; set; }
    public string? LatestPaymentTransactionReference { get; set; }
    public bool HasPaymentToVerify { get; set; }
    public bool CanStaffProcess { get; set; }
    public string? SpecialRequests { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? PaymentConfirmedByName { get; set; }
    public DateTime? PaymentConfirmedAt { get; set; }
    public string? BookingConfirmedByName { get; set; }
    public DateTime? BookingConfirmedAt { get; set; }
}
