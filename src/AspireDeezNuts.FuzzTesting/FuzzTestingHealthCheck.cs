using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspireDeezNuts.FuzzTesting;

public class FuzzTestingHealthCheck : IHealthCheck
{
    private static DateTime _lastTestCycleTime = DateTime.UtcNow;
    private static int _totalTestsRun = 0;
    private static int _failedTests = 0;
    private static bool _isRunning = true;

    public static void UpdateTestMetrics(int testsRun, int failed)
    {
        _lastTestCycleTime = DateTime.UtcNow;
        _totalTestsRun += testsRun;
        _failedTests += failed;
    }

    public static void SetRunningState(bool isRunning)
    {
        _isRunning = isRunning;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var timeSinceLastCycle = DateTime.UtcNow - _lastTestCycleTime;

        var data = new Dictionary<string, object>
        {
            ["LastTestCycle"] = _lastTestCycleTime.ToString("O"),
            ["TimeSinceLastCycle"] = timeSinceLastCycle.ToString(),
            ["TotalTestsRun"] = _totalTestsRun,
            ["FailedTests"] = _failedTests,
            ["IsRunning"] = _isRunning
        };

        if (!_isRunning)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Fuzz testing service is not running", data: data));
        }

        // If no test has run for more than 10 minutes, consider it degraded
        if (timeSinceLastCycle > TimeSpan.FromMinutes(10))
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"No test cycle completed in the last {timeSinceLastCycle.TotalMinutes:F1} minutes",
                data: data));
        }

        // If failure rate is too high, report as degraded
        if (_totalTestsRun > 0 && _failedTests > 0)
        {
            var failureRate = (double)_failedTests / _totalTestsRun;
            if (failureRate > 0.5) // More than 50% failure rate
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"High failure rate: {failureRate:P1}",
                    data: data));
            }
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Fuzz testing service is healthy. Last cycle: {timeSinceLastCycle.TotalSeconds:F1} seconds ago",
            data: data));
    }
}