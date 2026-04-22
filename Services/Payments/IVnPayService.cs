using Microsoft.AspNetCore.Http;

namespace ChillTour.Services.Payments;

public interface IVnPayService
{
    string CreatePaymentUrl(VnPayRequest request);
    VnPayResult ParseResponse(IQueryCollection query);
}
