using System.ComponentModel.DataAnnotations;

namespace ChillTour.Models.Admin;

public class TourDepartureEditorViewModel
{
    public long? TourScheduleId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày khởi hành.")]
    public DateOnly? DepartureDate { get; set; }

    [Range(typeof(decimal), "1", "999999999999", ErrorMessage = "Vui lòng nhập giá người lớn lớn hơn 0.")]
    public decimal AdultPrice { get; set; }

    public bool IsDeleted { get; set; }
}
