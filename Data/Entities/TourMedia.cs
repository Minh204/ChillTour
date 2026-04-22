namespace ChillTour.Data.Entities;

public class TourMedia
{
    public long TourMediaId { get; set; }
    public long TourId { get; set; }
    public byte MediaType { get; set; } = 1;
    public string MediaUrl { get; set; } = null!;
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }

    public Tour Tour { get; set; } = null!;
}
