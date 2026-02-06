using Benchmark.Core.Models;

namespace Benchmark.Core.Services;

public interface IBenchmarkRunner
{
    Task<BenchmarkSummary> RunBenchmarksAsync(string projectPath);
}
