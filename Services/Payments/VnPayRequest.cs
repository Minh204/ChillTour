namespace ChillTour.Services.Payments;

public class VnPayRequest
{
    public string TxnRef { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string OrderInfo { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime CreatedAtLocal { get; set; }
}
