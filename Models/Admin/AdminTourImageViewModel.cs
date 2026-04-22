namespace ChillTour.Models.Admin;

public class AdminTourImageViewModel
{
    public long TourMediaId { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
}
