using System.ComponentModel.DataAnnotations;
using ChillTour.Security;

namespace ChillTour.Models.Admin;

public class CreateUserViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn quyền.")]
    [RegularExpression($"{RoleConstants.Admin}|{RoleConstants.Director}|{RoleConstants.Manager}|{RoleConstants.Employee}|{RoleConstants.Customer}", ErrorMessage = "Quyền không hợp lệ.")]
    public string RoleCode { get; set; } = RoleConstants.Customer;
}
