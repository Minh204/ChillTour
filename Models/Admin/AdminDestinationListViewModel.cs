namespace ChillTour.Models.Admin;

public class AdminDestinationListViewModel
{
    public AdminDestinationsFilterViewModel Filter { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    public IReadOnlyList<AdminDestinationItemViewModel> Destinations { get; set; } = [];
}
