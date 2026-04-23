namespace ChillTour.Data.Entities;

public class Booking
{
    public long BookingId { get; set; }
    public string BookingCode { get; set; } = null!;
    public long UserId { get; set; }
    public long TourId { get; set; }
    public long TourScheduleId { get; set; }
    public string ContactName { get; set; } = null!;
    public string ContactEmail { get; set; } = null!;
    public string ContactPhone { get; set; } = null!;
    public int AdultCount { get; set; }
    public int ChildCount { get; set; }
    public int InfantCount { get; set; }
    public int SingleRoomCount { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal LastMinuteDiscountAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public byte BookingStatus { get; set; }
    public byte PaymentStatus { get; set; }
    public string? SpecialRequests { get; set; }
    public long? PromotionId { get; set; }
    public bool IsLastMinuteDeal { get; set; }
    public DateTime? BalanceDueAt { get; set; }
    public DateTime? BalanceReminderSentAt { get; set; }
    public DateTime? FullyPaidAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Tour Tour { get; set; } = null!;
    public TourSchedule TourSchedule { get; set; } = null!;
    public Promotion? Promotion { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
    public ICollection<ElectronicContract> ElectronicContracts { get; set; } = new List<ElectronicContract>();
}
