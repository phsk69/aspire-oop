using System.Net;
using System.Text.Json;

namespace AspireDeezNuts.Web.Tests.Helpers;

public class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode statusCode, object? response)> _responses = [];
    private readonly List<HttpRequestMessage> _requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    public void SetupResponse(string endpoint, HttpStatusCode statusCode, object? response = null)
    {
        _responses[endpoint] = (statusCode, response);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add(request);

        var endpoint = request.RequestUri?.PathAndQuery ?? "";

        if (_responses.TryGetValue(endpoint, out var setup))
        {
            var response = new HttpResponseMessage(setup.statusCode);

            if (setup.response != null)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };
                var json = JsonSerializer.Serialize(setup.response, options);
                response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}