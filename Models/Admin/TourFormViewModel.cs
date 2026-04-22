using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ChillTour.Models.Admin;

public class TourFormViewModel
{
    public long? TourId { get; set; }
    public string? TourCode { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên tour.")]
    [StringLength(250)]
    public string TourName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn điểm đi.")]
    public int StartDestinationId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn điểm đến.")]
    public int EndDestinationId { get; set; }

    public List<IFormFile> UploadedImages { get; set; } = [];
    public List<AdminTourImageViewModel> ExistingImages { get; set; } = [];
    public List<long> DeleteImageIds { get; set; } = [];

    [StringLength(1000)]
    public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    [Range(1, 365)]
    public int DurationDays { get; set; } = 1;

    [Range(0, 365)]
    public int DurationNights { get; set; }

    [Range(1, 1000)]
    public int MinGroupSize { get; set; } = 1;

    [Range(1, 1000)]
    public int? MaxGroupSize { get; set; }

    [Range(1, 100000)]
    public int TotalSeats { get; set; } = 20;

    [Range(0, 100000)]
    public int RemainingSeats { get; set; } = 20;

    [Range(0, double.MaxValue)]
    public decimal BasePrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ChildPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? SingleSupplement { get; set; }

    [StringLength(10)]
    public string CurrencyCode { get; set; } = "VND";

    [StringLength(200)]
    public string? DeparturePoint { get; set; }

    [StringLength(200)]
    public string? ReturnPoint { get; set; }

    public bool PickupIncluded { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsPublished { get; set; }

    [Range(0, 3)]
    public byte ApprovalStatus { get; set; }

    [StringLength(255)]
    public string? SeoTitle { get; set; }

    [StringLength(500)]
    public string? SeoDescription { get; set; }

    public List<SelectListItem> Categories { get; set; } = [];
    public List<SelectListItem> Destinations { get; set; } = [];
    public List<SelectListItem> Hotels { get; set; } = [];
    public List<SelectListItem> Transportations { get; set; } = [];
    public List<TourDepartureEditorViewModel> Schedules { get; set; } = [];
    public List<TourItineraryDayEditorViewModel> ItineraryDays { get; set; } = [];
}
