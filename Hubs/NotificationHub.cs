using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChillTour.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildUserGroup(userId));
        }

        await base.OnConnectedAsync();
    }

    public static string BuildUserGroup(long userId) => BuildUserGroup(userId.ToString());

    private static string BuildUserGroup(string userId) => $"user:{userId}";
}
