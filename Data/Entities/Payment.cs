namespace ChillTour.Data.Entities;

public class Payment
{
    public long PaymentId { get; set; }
    public long BookingId { get; set; }
    public string PaymentCode { get; set; } = null!;
    public byte PaymentMethod { get; set; }
    public string? PaymentGateway { get; set; }
    public string? TransactionReference { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public byte PaymentStatus { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
