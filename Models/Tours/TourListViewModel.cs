namespace ChillTour.Models.Tours;

public class TourListViewModel
{
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public int PageSize { get; set; }
    public int? SelectedDestinationId { get; set; }
    public DateOnly? SelectedDepartureDate { get; set; }
    public string? SelectedBudgetRange { get; set; }
    public string? SelectedSortBy { get; set; }
    public bool LastMinuteOnly { get; set; }
    public List<TourListItemViewModel> Tours { get; set; } = [];
}
