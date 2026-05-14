namespace ChillTour.Models.Payments;

public class SePayPaymentViewModel
{
    public long BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string TourName { get; set; } = string.Empty;
    public string PaymentCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransferContent { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNo { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
    public string QrImageUrl { get; set; } = string.Empty;
}
