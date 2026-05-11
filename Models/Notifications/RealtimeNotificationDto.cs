namespace ChillTour.Models.Notifications;

public class RealtimeNotificationDto
{
    public long NotificationId { get; set; }
    public byte NotificationType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public long? RelatedEntityId { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}
