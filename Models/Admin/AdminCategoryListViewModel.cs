namespace ChillTour.Models.Admin;

public class AdminCategoryListViewModel
{
    public AdminCategoriesFilterViewModel Filter { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    public IReadOnlyList<AdminCategoryItemViewModel> Categories { get; set; } = [];
}
