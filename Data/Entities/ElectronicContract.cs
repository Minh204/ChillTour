namespace ChillTour.Data.Entities;

public class ElectronicContract
{
    public long ElectronicContractId { get; set; }
    public long BookingId { get; set; }
    public string ContractCode { get; set; } = null!;
    public byte ContractStatus { get; set; }
    public string ContractHtml { get; set; } = string.Empty;
    public string? DraftPdfPath { get; set; }
    public string? FinalPdfPath { get; set; }
    public string? CustomerSignatureDataUrl { get; set; }
    public DateTime? CustomerSignedAt { get; set; }
    public string? CustomerSignedIp { get; set; }
    public string? CustomerSignedUserAgent { get; set; }
    public string? CustomerOtpHash { get; set; }
    public DateTime? CustomerOtpExpiresAt { get; set; }
    public string? DirectorSignatureDataUrl { get; set; }
    public DateTime? DirectorSignedAt { get; set; }
    public string? DirectorSignedIp { get; set; }
    public string? DirectorSignedUserAgent { get; set; }
    public string? DirectorOtpHash { get; set; }
    public DateTime? DirectorOtpExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public Booking Booking { get; set; } = null!;
}
