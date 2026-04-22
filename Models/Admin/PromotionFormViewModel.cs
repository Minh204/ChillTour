using System.ComponentModel.DataAnnotations;

namespace ChillTour.Models.Admin;

public class PromotionFormViewModel
{
    public long? PromotionId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã ưu đãi.")]
    [StringLength(50)]
    public string PromotionCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên sự kiện ưu đãi.")]
    [StringLength(200)]
    public string PromotionName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, 100, ErrorMessage = "Phần trăm giảm giá phải từ 0 đến 100.")]
    public decimal? DiscountPercent { get; set; }

    [Range(0, 999999999, ErrorMessage = "Giảm tối đa không hợp lệ.")]
    public decimal? MaxDiscountAmount { get; set; }

    [Range(0, 999999999, ErrorMessage = "Giá trị đơn tối thiểu không hợp lệ.")]
    public decimal? MinOrderValue { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu.")]
    public DateTime StartAt { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Vui lòng chọn thời gian kết thúc.")]
    public DateTime EndAt { get; set; } = DateTime.Now.AddMonths(1);

    public bool IsActive { get; set; } = true;
}
