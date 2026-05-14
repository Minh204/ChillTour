namespace ChillTour.Data.Entities;

public class Wishlist
{
    public long WishlistId { get; set; }
    public long UserId { get; set; }
    public long TourId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Tour Tour { get; set; } = null!;
}
