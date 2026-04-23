namespace ChillTour.Models.Account;

public class CustomerNotificationItemViewModel
{
    public long NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RelatedEntityType { get; set; }
    public long? RelatedEntityId { get; set; }
    public byte? RelatedBookingStatus { get; set; }
    public bool IsRelatedBookingCancelled => RelatedEntityType == "Booking" && RelatedBookingStatus == 4;
}
