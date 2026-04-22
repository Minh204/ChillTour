using System.ComponentModel.DataAnnotations;

namespace ChillTour.Models.Admin;

public class TourScheduleEditorViewModel
{
    public long? TourScheduleId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày khởi hành.")]
    public DateOnly? DepartureDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc.")]
    public DateOnly? ReturnDate { get; set; }

    [Range(1, 100000, ErrorMessage = "Tổng số vé phải lớn hơn 0.")]
    public int TotalSeats { get; set; } = 20;

    [Range(0, 100000, ErrorMessage = "Số vé còn lại không hợp lệ.")]
    public int AvailableSeats { get; set; } = 20;

    public int ReservedSeats { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá người lớn không hợp lệ.")]
    public decimal AdultPrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá trẻ em không hợp lệ.")]
    public decimal? ChildPrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá em bé không hợp lệ.")]
    public decimal? InfantPrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Phụ thu phòng đơn không hợp lệ.")]
    public decimal? SingleSupplement { get; set; }

    [Range(0, 4)]
    public byte Status { get; set; } = 1;

    public bool IsDeleted { get; set; }
}
