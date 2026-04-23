namespace ChillTour.Models.Payments;

public class PaymentCheckoutViewModel
{
    public long BookingId { get; set; }
    public long PaymentId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string TourName { get; set; } = string.Empty;
    public DateOnly DepartureDate { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public int Travelers { get; set; }
    public decimal BaseAmount { get; set; }
    public int SingleRoomCount { get; set; }
    public decimal SingleRoomSupplementAmount { get; set; }
    public decimal LastMinuteDiscountAmount { get; set; }
    public bool IsLastMinuteDeal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal FullPaymentAmount { get; set; }
    public bool MustPayFull { get; set; }
    public bool CanPayDeposit { get; set; }
    public bool CanPayFull { get; set; }
    public bool IsFullyPaid { get; set; }
    public DateTime? BalanceDueAt { get; set; }
    public byte BookingStatus { get; set; }
    public byte PaymentStatus { get; set; }
    public bool CanPay { get; set; }
    public bool CanCancelPendingBooking { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public string TransferContent { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNo { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
    public string QrImageUrl { get; set; } = string.Empty;
    public string PromotionCodeInput { get; set; } = string.Empty;
    public string? AppliedPromotionCode { get; set; }
    public string? AppliedPromotionName { get; set; }
    public List<PaymentPromotionOptionViewModel> AvailablePromotions { get; set; } = [];
}

public class PaymentPromotionOptionViewModel
{
    public string PromotionCode { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;
}
