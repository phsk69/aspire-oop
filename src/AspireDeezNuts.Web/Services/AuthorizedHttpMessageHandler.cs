namespace AspireDeezNuts.Web.Services;

public class AuthorizedHttpMessageHandler(IHttpContextAccessor httpContextAccessor, ILogger<AuthorizedHttpMessageHandler> logger) : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger<AuthorizedHttpMessageHandler> _logger = logger;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        _logger.LogInformation("AuthorizedHttpMessageHandler: HttpContext is null: {IsNull}", httpContext == null);

        if (httpContext != null && httpContext.User.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("AuthorizedHttpMessageHandler: User authenticated: {IsAuthenticated}", httpContext.User.Identity?.IsAuthenticated);
            _logger.LogInformation("AuthorizedHttpMessageHandler: User name: {UserName}", httpContext.User.Identity?.Name);

            // Get JWT token from user claims (stored during login)
            var jwtToken = httpContext.User.Claims.FirstOrDefault(c => c.Type == "access_token")?.Value;

            if (!string.IsNullOrEmpty(jwtToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
                _logger.LogInformation("AuthorizedHttpMessageHandler: Added Bearer token to request");
            }
            else
            {
                _logger.LogWarning("AuthorizedHttpMessageHandler: No JWT token found in user claims");
            }
        }

        var response = await base.SendAsync(request, cancellationToken);
        _logger.LogInformation("AuthorizedHttpMessageHandler: Response status: {StatusCode}", response.StatusCode);
        return response;
    }
}