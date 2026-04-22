namespace ChillTour.Data.Entities;

public class Notification
{
    public long NotificationId { get; set; }
    public long UserId { get; set; }
    public byte NotificationType { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? RelatedEntityType { get; set; }
    public long? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
