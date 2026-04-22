namespace ChillTour.Models.Admin;

public class AdminUsersViewModel
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int LockedUsers { get; set; }
    public AdminUsersFilterViewModel Filter { get; set; } = new();
    public IReadOnlyCollection<AdminUserItemViewModel> Users { get; set; } = Array.Empty<AdminUserItemViewModel>();
}
