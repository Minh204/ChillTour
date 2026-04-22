namespace ChillTour.Models.Admin;

public class AdminPromotionListViewModel
{
    public int TotalPromotions { get; set; }
    public int ActivePromotions { get; set; }
    public int ExpiredPromotions { get; set; }
    public IReadOnlyList<AdminPromotionItemViewModel> Promotions { get; set; } = [];
}
