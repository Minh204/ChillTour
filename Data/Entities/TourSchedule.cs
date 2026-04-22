namespace ChillTour.Data.Entities;

public class TourSchedule
{
    public long TourScheduleId { get; set; }
    public long TourId { get; set; }
    public string ScheduleCode { get; set; } = null!;
    public DateOnly DepartureDate { get; set; }
    public DateOnly ReturnDate { get; set; }
    public DateTime? BookingOpenAt { get; set; }
    public DateTime? BookingCloseAt { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public int ReservedSeats { get; set; }
    public decimal AdultPrice { get; set; }
    public decimal? ChildPrice { get; set; }
    public decimal? InfantPrice { get; set; }
    public decimal? SingleSupplement { get; set; }
    public byte Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Tour Tour { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
