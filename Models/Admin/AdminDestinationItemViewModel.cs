namespace ChillTour.Models.Admin;

public class AdminDestinationItemViewModel
{
    public int DestinationId { get; set; }
    public string DestinationCode { get; set; } = string.Empty;
    public string DestinationName { get; set; } = string.Empty;
    public byte DestinationType { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string? ProvinceName { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; }
}
