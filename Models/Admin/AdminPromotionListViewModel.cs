namespace ChillTour.Models.Admin;

public class AdminPromotionListViewModel
{
    public int TotalPromotions { get; set; }
    public int ActivePromotions { get; set; }
    public int ExpiredPromotions { get; set; }
    public AdminPromotionsFilterViewModel Filter { get; set; } = new();
    public IReadOnlyList<AdminPromotionItemViewModel> Promotions { get; set; } = [];
}
