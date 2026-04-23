namespace ChillTour.Models.Admin;

public class AdminTourItemViewModel
{
    public long TourId { get; set; }
    public string TourCode { get; set; } = string.Empty;
    public string TourName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsPublished { get; set; }
    public bool IsFeatured { get; set; }
    public DateOnly? NextDepartureDate { get; set; }
    public bool HasOnlyExpiredSchedules { get; set; }
    public DateTime CreatedAt { get; set; }
}
