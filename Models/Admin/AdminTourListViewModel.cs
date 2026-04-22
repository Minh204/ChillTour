namespace ChillTour.Models.Admin;

public class AdminTourListViewModel
{
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public IReadOnlyCollection<AdminTourItemViewModel> Tours { get; set; } = Array.Empty<AdminTourItemViewModel>();
}
