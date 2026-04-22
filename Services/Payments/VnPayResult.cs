namespace ChillTour.Services.Payments;

public class VnPayResult
{
    public bool IsValidSignature { get; set; }
    public bool IsSuccess { get; set; }
    public string TxnRef { get; set; } = string.Empty;
    public string? ResponseCode { get; set; }
    public string? TransactionStatus { get; set; }
    public string? TransactionNo { get; set; }
    public string? OrderInfo { get; set; }
    public long Amount { get; set; }
    public IReadOnlyDictionary<string, string> RawData { get; set; } = new Dictionary<string, string>();
}
