namespace ChillTour.Data.Entities;

public class TransportationType
{
    public int TransportationTypeId { get; set; }
    public string TypeCode { get; set; } = null!;
    public string TypeName { get; set; } = null!;

    public ICollection<Transportation> Transportations { get; set; } = new List<Transportation>();
}
