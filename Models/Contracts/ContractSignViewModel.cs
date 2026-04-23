namespace ChillTour.Models.Contracts;

public class ContractSignViewModel
{
    public long ContractId { get; set; }
    public string ContractCode { get; set; } = string.Empty;
    public string BookingCode { get; set; } = string.Empty;
    public string TourName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateOnly DepartureDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public byte ContractStatus { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string ContractHtml { get; set; } = string.Empty;
    public bool CanSign { get; set; }
    public bool IsDirectorSigning { get; set; }
    public bool CustomerSigned { get; set; }
    public bool DirectorSigned { get; set; }
    public DateTime? CustomerSignedAt { get; set; }
    public DateTime? DirectorSignedAt { get; set; }
    public string? PdfPath { get; set; }
    public string? DevOtp { get; set; }
}
