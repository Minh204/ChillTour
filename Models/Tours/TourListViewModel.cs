namespace ChillTour.Models.Tours;

public class TourListViewModel
{
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public int PageSize { get; set; }
    public int FeaturedCount { get; set; }
    public int LastMinuteCount { get; set; }
    public decimal? LowestPrice { get; set; }
    public DateOnly? EarliestDepartureDate { get; set; }
    public int? SelectedDestinationId { get; set; }
    public int? SelectedCategoryId { get; set; }
    public DateOnly? SelectedDepartureDate { get; set; }
    public string? SelectedBudgetRange { get; set; }
    public string? SelectedSortBy { get; set; }
    public string? SearchTerm { get; set; }
    public bool LastMinuteOnly { get; set; }
    public string? SelectedUrgencyFilter { get; set; }
    public List<TourFilterOptionViewModel> DestinationOptions { get; set; } = [];
    public List<TourFilterOptionViewModel> CategoryOptions { get; set; } = [];
    public List<TourListItemViewModel> Tours { get; set; } = [];
}

public class TourFilterOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
