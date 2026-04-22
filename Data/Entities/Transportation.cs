namespace ChillTour.Data.Entities;

public class Transportation
{
    public long TransportationId { get; set; }
    public string TransportationCode { get; set; } = null!;
    public int TransportationTypeId { get; set; }
    public string ProviderName { get; set; } = null!;
    public string? VehicleName { get; set; }
    public int? SeatCapacity { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    public TransportationType TransportationType { get; set; } = null!;
}
