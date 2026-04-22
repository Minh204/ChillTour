namespace ChillTour.Models.Promotions;

public class PromotionListItemViewModel
{
    public long PromotionId { get; set; }
    public string PromotionCode { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinOrderValue { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsAutoApply { get; set; }
    public bool IsClaimed { get; set; }
}
