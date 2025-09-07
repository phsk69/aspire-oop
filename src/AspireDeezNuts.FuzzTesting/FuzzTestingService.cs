namespace AspireDeezNuts.FuzzTesting;

public class FuzzTestingService(
    ILogger<FuzzTestingService> logger,
    IConfiguration configuration,
    IFuzzTestRunner fuzzTestRunner) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Fuzz Testing Service started");
        FuzzTestingHealthCheck.SetRunningState(true);
        
        var cycleDelayMinutes = configuration.GetValue<int>("FuzzTesting:CycleDelayMinutes", 5);
        var cycleDelay = TimeSpan.FromMinutes(cycleDelayMinutes);
        
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    logger.LogInformation("Running fuzz test cycle...");
                    await fuzzTestRunner.RunTestCycleAsync(stoppingToken);
                    
                    logger.LogInformation("Fuzz test cycle completed. Next cycle in {Minutes} minutes", cycleDelayMinutes);
                    
                    // Wait before next cycle
                    await Task.Delay(cycleDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during fuzz test cycle");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }
        finally
        {
            FuzzTestingHealthCheck.SetRunningState(false);
            logger.LogInformation("Fuzz Testing Service stopped");
        }
    }
}