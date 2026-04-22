namespace ChillTour.Data.Entities;

public class UserPromotion
{
    public long UserPromotionId { get; set; }
    public long UserId { get; set; }
    public long PromotionId { get; set; }
    public DateTime ClaimedAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
    public Promotion Promotion { get; set; } = null!;
}
