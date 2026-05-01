using System.ComponentModel.DataAnnotations;

namespace ChillTour.Models.Home;

public class ContactFormViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn loại thông tin.")]
    public string ContactType { get; set; } = "Du lịch";

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(160)]
    public string? CompanyName { get; set; }

    [Range(0, 10000, ErrorMessage = "Số khách không hợp lệ.")]
    public int GuestCount { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề.")]
    [StringLength(180)]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung.")]
    [StringLength(2000)]
    public string Message { get; set; } = string.Empty;
}
