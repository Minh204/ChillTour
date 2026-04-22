namespace ChillTour.Models.Account;

public class AccountProfilePageViewModel
{
    public ProfileViewModel Profile { get; set; } = new();
    public ChangePasswordViewModel ChangePassword { get; set; } = new();
}
