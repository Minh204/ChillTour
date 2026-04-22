namespace ChillTour.Services.Notifications;

public interface INotificationService
{
    Task CreateAsync(long userId, byte notificationType, string title, string message, string? relatedEntityType = null, long? relatedEntityId = null, CancellationToken cancellationToken = default);
    Task CreateForRolesAsync(IEnumerable<string> roleCodes, byte notificationType, string title, string message, string? relatedEntityType = null, long? relatedEntityId = null, CancellationToken cancellationToken = default);
}
