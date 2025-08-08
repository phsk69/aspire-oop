namespace AspireDeezNuts.Web.Services;

public class AuthenticationOptions
{
    public int TokenRefreshIntervalMinutes { get; set; } = 5;
}