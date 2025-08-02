using System.Net.Http.Headers;

namespace AspireDeezNuts.Web.Services;

public class AuthorizedHttpMessageHandler(IAuthService authService) : DelegatingHandler
{
    private readonly IAuthService _authService = authService;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Get the token
        var token = await _authService.GetTokenAsync();

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}