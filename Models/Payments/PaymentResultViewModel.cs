namespace ChillTour.Models.Payments;

public class PaymentResultViewModel
{
    public long BookingId { get; set; }
    public bool IsSuccess { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string PaymentCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? TransactionReference { get; set; }
    public decimal Amount { get; set; }
}
