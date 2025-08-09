using System.Collections.Concurrent;

namespace AspireDeezNuts.Web.Services;

/// <summary>
/// Service for rate limiting operations to prevent rapid successive calls
/// </summary>
public interface IRateLimitingService
{
    /// <summary>
    /// Checks if an operation is allowed based on rate limiting rules
    /// </summary>
    /// <param name="operationKey">Unique key for the operation (e.g., "token-refresh", "user-{userId}-action")</param>
    /// <param name="timeWindow">Time window for rate limiting</param>
    /// <param name="maxAttempts">Maximum attempts allowed within the time window</param>
    /// <returns>True if operation is allowed, false if rate limited</returns>
    bool IsOperationAllowed(string operationKey, TimeSpan timeWindow, int maxAttempts = 1);

    /// <summary>
    /// Executes an operation if rate limiting allows it
    /// </summary>
    /// <param name="operationKey">Unique key for the operation</param>
    /// <param name="operation">Operation to execute</param>
    /// <param name="timeWindow">Time window for rate limiting</param>
    /// <param name="maxAttempts">Maximum attempts allowed within the time window</param>
    /// <returns>True if operation was executed, false if rate limited</returns>
    Task<bool> TryExecuteAsync(string operationKey, Func<Task> operation, TimeSpan timeWindow, int maxAttempts = 1);

    /// <summary>
    /// Executes an operation with return value if rate limiting allows it
    /// </summary>
    /// <param name="operationKey">Unique key for the operation</param>
    /// <param name="operation">Operation to execute</param>
    /// <param name="timeWindow">Time window for rate limiting</param>
    /// <param name="maxAttempts">Maximum attempts allowed within the time window</param>
    /// <returns>Operation result if executed, default(T) if rate limited</returns>
    Task<(bool executed, T? result)> TryExecuteAsync<T>(string operationKey, Func<Task<T>> operation, TimeSpan timeWindow, int maxAttempts = 1);

    /// <summary>
    /// Clears rate limiting data for a specific operation
    /// </summary>
    /// <param name="operationKey">Operation key to clear</param>
    void ClearOperationHistory(string operationKey);
}

/// <summary>
/// Microsoft-standard rate limiting service implementation for Blazor Server
/// </summary>
public class RateLimitingService : IRateLimitingService, IDisposable
{
    private readonly ConcurrentDictionary<string, OperationHistory> _operationHistory = new();
    private readonly ILogger<RateLimitingService> _logger;
    private readonly Timer _cleanupTimer;

    public RateLimitingService(ILogger<RateLimitingService> logger)
    {
        _logger = logger;
        
        // Clean up expired entries every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public bool IsOperationAllowed(string operationKey, TimeSpan timeWindow, int maxAttempts = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);

        var now = DateTime.UtcNow;
        var history = _operationHistory.GetOrAdd(operationKey, _ => new OperationHistory());

        lock (history.Lock)
        {
            // Remove expired attempts
            var cutoffTime = now - timeWindow;
            history.Attempts.RemoveAll(attempt => attempt < cutoffTime);

            // Check if we're within limits
            if (history.Attempts.Count >= maxAttempts)
            {
                _logger.LogWarning("Rate limit exceeded for operation '{OperationKey}'. {AttemptCount}/{MaxAttempts} attempts in {TimeWindow}", 
                    operationKey, history.Attempts.Count, maxAttempts, timeWindow);
                return false;
            }

            // Record this attempt
            history.Attempts.Add(now);
            _logger.LogDebug("Operation '{OperationKey}' allowed. {AttemptCount}/{MaxAttempts} attempts in {TimeWindow}", 
                operationKey, history.Attempts.Count, maxAttempts, timeWindow);
            return true;
        }
    }

    public async Task<bool> TryExecuteAsync(string operationKey, Func<Task> operation, TimeSpan timeWindow, int maxAttempts = 1)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!IsOperationAllowed(operationKey, timeWindow, maxAttempts))
        {
            return false;
        }

        try
        {
            await operation();
            _logger.LogDebug("Operation '{OperationKey}' executed successfully", operationKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation '{OperationKey}' failed during execution", operationKey);
            throw;
        }
    }

    public async Task<(bool executed, T? result)> TryExecuteAsync<T>(string operationKey, Func<Task<T>> operation, TimeSpan timeWindow, int maxAttempts = 1)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!IsOperationAllowed(operationKey, timeWindow, maxAttempts))
        {
            return (false, default(T));
        }

        try
        {
            var result = await operation();
            _logger.LogDebug("Operation '{OperationKey}' executed successfully with result", operationKey);
            return (true, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation '{OperationKey}' failed during execution", operationKey);
            throw;
        }
    }

    public void ClearOperationHistory(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

        if (_operationHistory.TryRemove(operationKey, out _))
        {
            _logger.LogDebug("Cleared rate limiting history for operation '{OperationKey}'", operationKey);
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var keysToRemove = new List<string>();

            foreach (var kvp in _operationHistory)
            {
                var history = kvp.Value;
                lock (history.Lock)
                {
                    // Remove attempts older than 1 hour
                    var cutoffTime = now - TimeSpan.FromHours(1);
                    history.Attempts.RemoveAll(attempt => attempt < cutoffTime);

                    // If no recent attempts, mark for removal
                    if (history.Attempts.Count == 0)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                _operationHistory.TryRemove(key, out _);
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired rate limiting entries", keysToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rate limiting cleanup");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    private class OperationHistory
    {
        public List<DateTime> Attempts { get; } = [];
        public object Lock { get; } = new();
    }
}