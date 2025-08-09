namespace AspireDeezNuts.Web.Tests.Helpers;

public class TestHttpClientFactory : IHttpClientFactory
{
    private readonly Dictionary<string, HttpClient> _clients = [];

    public void AddClient(string name, HttpClient client)
    {
        _clients[name] = client;
    }

    public HttpClient CreateClient(string name)
    {
        if (_clients.TryGetValue(name, out var client))
        {
            return client;
        }
        
        throw new InvalidOperationException($"No HttpClient configured for '{name}'");
    }
}