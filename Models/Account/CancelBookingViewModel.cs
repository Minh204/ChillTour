using System.ComponentModel.DataAnnotations;

namespace ChillTour.Models.Account;

public class CancelBookingViewModel
{
    [Required]
    public long BookingId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do hủy.")]
    [StringLength(500, ErrorMessage = "Lý do hủy không được vượt quá 500 ký tự.")]
    public string CancellationReason { get; set; } = string.Empty;
}
