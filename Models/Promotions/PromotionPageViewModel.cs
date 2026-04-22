namespace ChillTour.Models.Promotions;

public class PromotionPageViewModel
{
    public List<PromotionListItemViewModel> ActivePromotions { get; set; } = [];
    public List<PromotionListItemViewModel> MyPromotions { get; set; } = [];
    public bool IsCustomer { get; set; }
}
