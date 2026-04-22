using Microsoft.Extensions.Options;

namespace ChillTour.Services.Payments;

public class VietQrService : IVietQrService
{
    private readonly VietQrOptions _options;

    public VietQrService(IOptions<VietQrOptions> options)
    {
        _options = options.Value;
    }

    public string BuildQrImageUrl(decimal amount, string transferContent)
    {
        var roundedAmount = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
        var query = $"amount={Uri.EscapeDataString(roundedAmount.ToString("0"))}" +
                    $"&addInfo={Uri.EscapeDataString(transferContent)}" +
                    $"&accountName={Uri.EscapeDataString(_options.AccountName)}";

        return $"https://img.vietqr.io/image/{_options.BankId}-{_options.AccountNo}-{_options.Template}.png?{query}";
    }
}
