namespace ChillTour.Services.Payments;

public interface IVietQrService
{
    string BuildQrImageUrl(decimal amount, string transferContent);
}
