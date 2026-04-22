using System.ComponentModel.DataAnnotations;

namespace ChillTour.Models.Tours;

public class TourBookingFormViewModel
{
    [Required]
    public long TourId { get; set; }

    [Required]
    public long TourScheduleId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên liên hệ.")]
    [StringLength(150)]
    public string ContactName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email liên hệ.")]
    [EmailAddress]
    [StringLength(255)]
    public string ContactEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [StringLength(20)]
    public string ContactPhone { get; set; } = string.Empty;

    [Range(1, 20, ErrorMessage = "Cần ít nhất 1 người lớn.")]
    public int AdultCount { get; set; } = 1;

    [Range(0, 20)]
    public int ChildCount { get; set; }

    [Range(0, 20)]
    public int InfantCount { get; set; }

    [Range(0, 20)]
    public int SingleRoomCount { get; set; }

    [StringLength(1000)]
    public string? SpecialRequests { get; set; }
}
