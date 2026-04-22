using ChillTour.Data;
using ChillTour.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChillTour.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly ChillTourDbContext _dbContext;

    public NotificationService(ChillTourDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateAsync(long userId, byte notificationType, string title, string message, string? relatedEntityType = null, long? relatedEntityId = null, CancellationToken cancellationToken = default)
    {
        _dbContext.Notifications.Add(new Notification
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
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
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
        foreach (var userId in userIds)
        {
            _dbContext.Notifications.Add(new Notification
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
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
