namespace ChillTour.Models.Admin;

public class AdminCategoryListViewModel
{
    public AdminCategoriesFilterViewModel Filter { get; set; } = new();
    public IReadOnlyList<AdminCategoryItemViewModel> Categories { get; set; } = [];
}
