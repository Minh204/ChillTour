namespace ChillTour.Services.Payments;

public class VietQrOptions
{
    public const string SectionName = "VietQr";

    public string BankId { get; set; } = "vietinbank";
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Template { get; set; } = "compact2";
}
