namespace AspireDeezNuts.FuzzTesting;

public class FuzzTestRunner(
    ILogger<FuzzTestRunner> logger,
    IEnumerable<IFuzzTest> fuzzTests) : IFuzzTestRunner
{
    public async Task RunTestCycleAsync(CancellationToken cancellationToken)
    {
        var tasks = new List<Task<FuzzTestResult>>();

        foreach (var test in fuzzTests)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            tasks.Add(RunTestAsync(test, cancellationToken));
        }

        var results = await Task.WhenAll(tasks);

        // Update health check metrics
        var totalTests = results.Length;
        var failedTests = results.Count(r => !r.IsSuccess);
        FuzzTestingHealthCheck.UpdateTestMetrics(totalTests, failedTests);
    }

    private async Task<FuzzTestResult> RunTestAsync(IFuzzTest test, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting fuzz test: {TestName}", test.Name);
            var result = await test.ExecuteAsync(cancellationToken);

            if (result.IsSuccess)
            {
                logger.LogInformation("Fuzz test {TestName} completed successfully. Iterations: {Iterations}",
                    test.Name, result.Iterations);
            }
            else
            {
                logger.LogWarning("Fuzz test {TestName} found issues: {Issues}",
                    test.Name, result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running fuzz test {TestName}", test.Name);
            return new FuzzTestResult(false, 0, $"Exception: {ex.Message}");
        }
    }
}