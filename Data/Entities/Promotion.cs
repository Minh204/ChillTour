namespace ChillTour.Data.Entities;

public class Promotion
{
    public long PromotionId { get; set; }
    public string PromotionCode { get; set; } = null!;
    public string PromotionName { get; set; } = null!;
    public byte PromotionType { get; set; }
    public string? Description { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinOrderValue { get; set; }
    public int? MaxUsageCount { get; set; }
    public int? MaxUsagePerUser { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsAutoApply { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<UserPromotion> UserPromotions { get; set; } = new List<UserPromotion>();
}
