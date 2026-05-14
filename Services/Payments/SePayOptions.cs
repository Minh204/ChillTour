namespace ChillTour.Services.Payments;

public class SePayOptions
{
    public const string SectionName = "SePay";

    public string ApiKey { get; set; } = string.Empty;
    public string BankName { get; set; } = "VietinBank";
    public string AccountNumber { get; set; } = "100877669207";
    public string AccountName { get; set; } = "NGUYEN VAN MINH";
    public string PaymentCodePrefix { get; set; } = "PAY";
    public string QrTemplate { get; set; } = "compact";
}
