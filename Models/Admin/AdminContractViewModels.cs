namespace ChillTour.Models.Admin;

public class AdminContractListViewModel
{
    public int TotalContracts { get; set; }
    public int PendingDirectorSign { get; set; }
    public int PendingCustomerSign { get; set; }
    public int SignedContracts { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    public IReadOnlyList<AdminContractItemViewModel> Contracts { get; set; } = [];
}

public class AdminContractItemViewModel
{
    public long ContractId { get; set; }
    public string ContractCode { get; set; } = string.Empty;
    public byte ContractStatus { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string BookingCode { get; set; } = string.Empty;
    public string TourName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateOnly DepartureDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DirectorSignedAt { get; set; }
    public DateTime? CustomerSignedAt { get; set; }
    public bool CanDirectorSign { get; set; }
    public bool IsExpiredCancelled { get; set; }
}
