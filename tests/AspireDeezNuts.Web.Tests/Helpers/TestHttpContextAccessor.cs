using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AspireDeezNuts.Web.Tests.Helpers;

public class TestHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }

    public void SetAuthenticatedUser(string email, string userId, string[] roles, string? accessToken = null, string? refreshToken = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email),
            new(ClaimTypes.NameIdentifier, userId)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (accessToken != null)
        {
            claims.Add(new Claim("access_token", accessToken));
        }

        if (refreshToken != null)
        {
            claims.Add(new Claim("refresh_token", refreshToken));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext();
        context.User = principal;
        context.Items = new TestItemsDictionary();
        
        HttpContext = context;
    }

    public void SetAnonymousUser()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        
        // Use a custom dictionary that returns null instead of throwing for missing keys
        context.Items = new TestItemsDictionary();
        
        HttpContext = context;
    }
}