using ChillTour.Data.Entities;

namespace ChillTour.Services.Contracts;

public interface IContractService
{
    Task<ElectronicContract> EnsureContractForBookingAsync(long bookingId, CancellationToken cancellationToken = default);
    Task<string> SendOtpAsync(ElectronicContract contract, long actorUserId, bool isDirector, CancellationToken cancellationToken = default);
    Task<bool> SignAsync(ElectronicContract contract, long actorUserId, bool isDirector, string otp, string signatureDataUrl, string ipAddress, string userAgent, CancellationToken cancellationToken = default);
    byte[] GeneratePdf(ElectronicContract contract);
}
