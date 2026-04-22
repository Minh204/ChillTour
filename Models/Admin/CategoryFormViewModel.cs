using System.ComponentModel.DataAnnotations;

namespace ChillTour.Models.Admin;

public class CategoryFormViewModel
{
    public int? CategoryId { get; set; }
    public string? CategoryCode { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên danh mục.")]
    [StringLength(150)]
    public string CategoryName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(0, 9999)]
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
