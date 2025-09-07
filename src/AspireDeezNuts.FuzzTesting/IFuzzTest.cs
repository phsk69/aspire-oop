namespace AspireDeezNuts.FuzzTesting;

public interface IFuzzTest
{
    string Name { get; }
    Task<FuzzTestResult> ExecuteAsync(CancellationToken cancellationToken);
}

public record FuzzTestResult(
    bool IsSuccess,
    int Iterations,
    string? ErrorMessage = null,
    Dictionary<string, object>? Metadata = null);