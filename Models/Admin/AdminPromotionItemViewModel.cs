namespace ChillTour.Models.Admin;

public class AdminPromotionItemViewModel
{
    public long PromotionId { get; set; }
    public string PromotionCode { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountAmount { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsActive { get; set; }
    public string? BannerImageUrl { get; set; }
    public bool ShowOnHomeBanner { get; set; }
    public int BannerDisplayOrder { get; set; }
    public int ClaimedCount { get; set; }
    public int UsedCount { get; set; }
}
