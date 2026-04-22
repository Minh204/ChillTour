namespace ChillTour.Data.Entities;

public class Destination
{
    public int DestinationId { get; set; }
    public int? ParentDestinationId { get; set; }
    public string DestinationCode { get; set; } = null!;
    public byte DestinationType { get; set; }
    public string DestinationName { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string CountryCode { get; set; } = null!;
    public string? ProvinceName { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Destination? ParentDestination { get; set; }
    public ICollection<Destination> Children { get; set; } = new List<Destination>();
    public ICollection<Hotel> Hotels { get; set; } = new List<Hotel>();
}
