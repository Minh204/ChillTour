namespace ChillTour.Models.Admin;

public class AdminCategoryItemViewModel
{
    public int CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public int TourCount { get; set; }
}
