using ChillTour.Data;
using ChillTour.Data.Entities;
using ChillTour.Hubs;
using ChillTour.Models.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace ChillTour.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly ChillTourDbContext _dbContext;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(ChillTourDbContext dbContext, IHubContext<NotificationHub> hubContext)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
    }

    public async Task CreateAsync(long userId, byte notificationType, string title, string message, string? relatedEntityType = null, long? relatedEntityId = null, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            NotificationType = notificationType,
            Title = title,
            Message = message,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            IsRead = false,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await SendRealtimeAsync(notification, cancellationToken);
    }

    public async Task CreateForRolesAsync(IEnumerable<string> roleCodes, byte notificationType, string title, string message, string? relatedEntityType = null, long? relatedEntityId = null, CancellationToken cancellationToken = default)
    {
        var normalizedRoleCodes = roleCodes
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var userIds = await _dbContext.UserRoles
            .Where(x => normalizedRoleCodes.Contains(x.Role.RoleCode.ToUpper()) && x.User.Status == 1)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
        {
            return;
        }

        var createdAt = DateTime.UtcNow;
        var notifications = new List<Notification>();
        foreach (var userId in userIds)
        {
            var notification = new Notification
            {
                UserId = userId,
                NotificationType = notificationType,
                Title = title,
                Message = message,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                IsRead = false,
                SentAt = createdAt,
                CreatedAt = createdAt
            };

            notifications.Add(notification);
            _dbContext.Notifications.Add(notification);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        foreach (var notification in notifications)
        {
            await SendRealtimeAsync(notification, cancellationToken);
        }
    }

    private Task SendRealtimeAsync(Notification notification, CancellationToken cancellationToken)
    {
        var payload = new RealtimeNotificationDto
        {
            NotificationId = notification.NotificationId,
            NotificationType = notification.NotificationType,
            Title = notification.Title,
            Message = notification.Message,
            RelatedEntityType = notification.RelatedEntityType,
            RelatedEntityId = notification.RelatedEntityId,
            CreatedAt = notification.CreatedAt.ToString("o")
        };

        return _hubContext.Clients
            .Group(NotificationHub.BuildUserGroup(notification.UserId))
            .SendAsync("ReceiveNotification", payload, cancellationToken);
    }
}
