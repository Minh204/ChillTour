namespace ChillTour.Data.Entities;

public class BookingStatusHistory
{
    public long BookingStatusHistoryId { get; set; }
    public long BookingId { get; set; }
    public byte? OldStatus { get; set; }
    public byte NewStatus { get; set; }
    public long? ChangedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime ChangedAt { get; set; }

    public Booking Booking { get; set; } = null!;
    public User? ChangedByUser { get; set; }
}
