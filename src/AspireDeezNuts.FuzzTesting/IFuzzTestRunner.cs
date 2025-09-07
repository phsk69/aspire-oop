namespace AspireDeezNuts.FuzzTesting;

public interface IFuzzTestRunner
{
    Task RunTestCycleAsync(CancellationToken cancellationToken);
}