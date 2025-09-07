using Microsoft.AspNetCore.SignalR;

namespace AspireDeezNuts.Web.Services;

/// <summary>
/// Service for broadcasting authentication state changes to connected clients via SignalR
/// </summary>
public interface IAuthStateNotificationService
{
    /// <summary>
    /// Notifies all connected clients for a user that their token has been refreshed
    /// </summary>
    /// <param name="userId">The user ID whose token was refreshed</param>
    /// <param name="newTokenInfo">Information about the new token</param>
    Task NotifyTokenRefreshedAsync(string userId, TokenInfo newTokenInfo);

    /// <summary>
    /// Notifies all connected clients for a user that their authentication state has changed
    /// </summary>
    /// <param name="userId">The user ID whose authentication state changed</param>
    /// <param name="isAuthenticated">Whether the user is still authenticated</param>
    Task NotifyAuthStateChangedAsync(string userId, bool isAuthenticated);
}

public class AuthStateNotificationService(
    IHubContext<Hubs.AuthHub> hubContext,
    ILogger<AuthStateNotificationService> logger) : IAuthStateNotificationService
{
    public async Task NotifyTokenRefreshedAsync(string userId, TokenInfo newTokenInfo)
    {
        try
        {
            var groupName = $"User_{userId}";
            logger.LogInformation("Broadcasting token refresh notification to group {GroupName}", groupName);
            
            await hubContext.Clients.Group(groupName).SendAsync("TokenRefreshed", new
            {
                newTokenInfo.ExpiresAt,
                newTokenInfo.IssuedAt,
                newTokenInfo.TimeUntilExpiry,
                newTokenInfo.IsExpired,
                RefreshedAt = DateTime.UtcNow
            });
            
            logger.LogDebug("Token refresh notification sent successfully to group {GroupName}", groupName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast token refresh notification for user {UserId}", userId);
        }
    }

    public async Task NotifyAuthStateChangedAsync(string userId, bool isAuthenticated)
    {
        try
        {
            var groupName = $"User_{userId}";
            logger.LogInformation("Broadcasting auth state change notification to group {GroupName}: {IsAuthenticated}", 
                groupName, isAuthenticated);
            
            await hubContext.Clients.Group(groupName).SendAsync("AuthStateChanged", new
            {
                IsAuthenticated = isAuthenticated,
                ChangedAt = DateTime.UtcNow
            });
            
            logger.LogDebug("Auth state change notification sent successfully to group {GroupName}", groupName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast auth state change notification for user {UserId}", userId);
        }
    }
}