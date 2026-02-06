namespace BenchmarkerRunner.Services;

public interface IBenchmarkOrchestrator
{
    Task<int> RunAllAsync(CancellationToken cancellationToken);
}
