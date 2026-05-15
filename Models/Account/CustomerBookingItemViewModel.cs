namespace ChillTour.Models.Account;

public class CustomerBookingItemViewModel
{
    public long BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string TourName { get; set; } = string.Empty;
    public string TourSlug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateOnly DepartureDate { get; set; }
    public int Travelers { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public byte BookingStatus { get; set; }
    public string BookingStatusText { get; set; } = string.Empty;
    public bool IsCompletedByDeparture { get; set; }
    public byte PaymentStatus { get; set; }
    public decimal PaidAmount { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? PaymentConfirmedByName { get; set; }
    public DateTime? PaymentConfirmedAt { get; set; }
    public string? BookingConfirmedByName { get; set; }
    public DateTime? BookingConfirmedAt { get; set; }
    public long? ContractId { get; set; }
    public string? ContractCode { get; set; }
    public byte? ContractStatus { get; set; }
    public bool CanCancel { get; set; }
    public bool CanRequestRefund { get; set; }
    public bool RequiresRefundRequest { get; set; }
    public int RefundPercent { get; set; }
    public decimal EstimatedRefundAmount { get; set; }
}
