namespace ChillTour.Models.Admin;

public class AdminDestinationListViewModel
{
    public AdminDestinationsFilterViewModel Filter { get; set; } = new();
    public IReadOnlyList<AdminDestinationItemViewModel> Destinations { get; set; } = [];
}
