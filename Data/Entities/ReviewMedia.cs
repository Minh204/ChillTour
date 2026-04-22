namespace ChillTour.Data.Entities;

public class ReviewMedia
{
    public long ReviewMediaId { get; set; }
    public long ReviewId { get; set; }
    public string MediaUrl { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public Review Review { get; set; } = null!;
}
