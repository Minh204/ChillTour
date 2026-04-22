using System.ComponentModel.DataAnnotations;

namespace ChillTour.Models.Admin;

public class UpdateBookingStatusViewModel
{
    public long BookingId { get; set; }
    public int CurrentPage { get; set; }

    [Range(0, 10)]
    public byte BookingStatus { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
