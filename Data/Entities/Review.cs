namespace ChillTour.Data.Entities;

public class Review
{
    public long ReviewId { get; set; }
    public long TourId { get; set; }
    public long UserId { get; set; }
    public long? BookingId { get; set; }
    public decimal Rating { get; set; }
    public string? Title { get; set; }
    public string? Comment { get; set; }
    public bool IsAnonymous { get; set; }
    public byte ModerationStatus { get; set; }
    public int HelpfulCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Tour Tour { get; set; } = null!;
    public User User { get; set; } = null!;
    public Booking? Booking { get; set; }
    public ICollection<ReviewMedia> MediaItems { get; set; } = new List<ReviewMedia>();
}
