namespace ChillTour.Models.Guides;

public class GuidePageViewModel
{
    public string? SearchTerm { get; set; }
    public int? SelectedYear { get; set; }
    public List<int> AvailableYears { get; set; } = [];
    public List<GuideListItemViewModel> Articles { get; set; } = [];
}
