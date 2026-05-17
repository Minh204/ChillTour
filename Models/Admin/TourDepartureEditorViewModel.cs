using System.ComponentModel.DataAnnotations;

namespace ChillTour.Models.Admin;

public class TourDepartureEditorViewModel
{
    public long? TourScheduleId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày khởi hành.")]
    public DateOnly? DepartureDate { get; set; }

    [Range(1, 100000, ErrorMessage = "Tổng số chỗ phải lớn hơn 0.")]
    public int TotalSeats { get; set; } = 20;

    [Range(0, 100000, ErrorMessage = "Số chỗ còn lại không hợp lệ.")]
    public int AvailableSeats { get; set; } = 20;

    [Range(typeof(decimal), "1", "999999999999", ErrorMessage = "Vui lòng nhập giá người lớn lớn hơn 0.")]
    public decimal AdultPrice { get; set; }

    public bool IsDeleted { get; set; }
}
