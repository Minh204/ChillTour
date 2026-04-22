using System.ComponentModel.DataAnnotations;

namespace ChillTour.Models.Admin;

public class TourItineraryDayEditorViewModel
{
    public long? ItineraryDayId { get; set; }

    [Range(1, 365, ErrorMessage = "Ngày thứ phải lớn hơn 0.")]
    public int DayNumber { get; set; } = 1;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề ngày.")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Summary { get; set; }

    public string? Description { get; set; }
    [StringLength(200)]
    public string? OvernightStay { get; set; }
    public bool BreakfastIncluded { get; set; }
    public bool LunchIncluded { get; set; }
    public bool DinnerIncluded { get; set; }
    [StringLength(200)]
    public string? HotelName { get; set; }
    [StringLength(200)]
    public string? TransportationName { get; set; }
    public long? HotelId { get; set; }
    public long? TransportationId { get; set; }
    public bool IsDeleted { get; set; }
}
