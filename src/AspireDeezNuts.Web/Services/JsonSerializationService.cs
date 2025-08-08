using System.Text.Json;

namespace AspireDeezNuts.Web.Services;

public interface IJsonSerializationService
{
    JsonSerializerOptions CamelCaseOptions { get; }
    T? Deserialize<T>(string json);
    string Serialize<T>(T value);
}

public class JsonSerializationService : IJsonSerializationService
{
    public JsonSerializerOptions CamelCaseOptions { get; }

    public JsonSerializationService()
    {
        CamelCaseOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, CamelCaseOptions);
    
    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, CamelCaseOptions);
}