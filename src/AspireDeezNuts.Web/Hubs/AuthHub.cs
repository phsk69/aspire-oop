using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AspireDeezNuts.Web.Hubs;

/// <summary>
/// SignalR hub for broadcasting authentication state changes to connected clients
/// </summary>
public class AuthHub(ILogger<AuthHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserIdentifier(Context.User);
        var userName = Context.User?.Identity?.Name;

        logger.LogInformation("User {UserName} (ID: {UserId}) connected to AuthHub", userName, userId);

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            logger.LogDebug("Added connection {ConnectionId} to group User_{UserId}", Context.ConnectionId, userId);
        }
        else
        {
            logger.LogWarning("Could not determine user ID for SignalR connection. Available claims: {Claims}",
                string.Join(", ", Context.User?.Claims.Select(c => $"{c.Type}={c.Value}") ?? []));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserIdentifier(Context.User);
        var userName = Context.User?.Identity?.Name;

        logger.LogInformation("User {UserName} (ID: {UserId}) disconnected from AuthHub", userName, userId);

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
            logger.LogDebug("Removed connection {ConnectionId} from group User_{UserId}", Context.ConnectionId, userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string? GetUserIdentifier(ClaimsPrincipal? user)
    {
        if (user == null) return null;

        // Try multiple claim types that could serve as user identifier
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst("email")?.Value
            ?? user.FindFirst("preferred_username")?.Value;
    }
}