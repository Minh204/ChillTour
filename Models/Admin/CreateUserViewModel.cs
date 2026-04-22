using System.ComponentModel.DataAnnotations;
using ChillTour.Security;

namespace ChillTour.Models.Admin;

public class CreateUserViewModel
{
    [Required(ErrorMessage = "Vui lÃ²ng nháº­p há» tÃªn.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lÃ²ng nháº­p email.")]
    [EmailAddress(ErrorMessage = "Email khÃ´ng há»£p lá»‡.")]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Vui lÃ²ng nháº­p máº­t kháº©u.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Máº­t kháº©u pháº£i tá»« 6 kÃ½ tá»± trá»Ÿ lÃªn.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lÃ²ng chá»n quyá»n.")]
    [RegularExpression($"{RoleConstants.Admin}|{RoleConstants.Director}|{RoleConstants.Manager}|{RoleConstants.Accountant}|{RoleConstants.Employee}|{RoleConstants.Customer}", ErrorMessage = "Quyá»n khÃ´ng há»£p lá»‡.")]
    public string RoleCode { get; set; } = RoleConstants.Customer;
}
