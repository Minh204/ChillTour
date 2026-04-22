using System.ComponentModel.DataAnnotations;

namespace ChillTour.Models.Admin;

public class DestinationFormViewModel
{
    public int? DestinationId { get; set; }
    public string? DestinationCode { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên điểm đến.")]
    [StringLength(150)]
    public string DestinationName { get; set; } = string.Empty;

    [Range(1, 4)]
    public byte DestinationType { get; set; } = 1;

    [Required(ErrorMessage = "Vui lòng nhập mã quốc gia.")]
    [StringLength(10)]
    public string CountryCode { get; set; } = "VN";

    [StringLength(100)]
    public string? ProvinceName { get; set; }

    [StringLength(500)]
    public string? Summary { get; set; }

    public string? Description { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
}
