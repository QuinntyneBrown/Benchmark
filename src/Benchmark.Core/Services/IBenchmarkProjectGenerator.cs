using Benchmark.Core.Models;

namespace Benchmark.Core.Services;

public interface IBenchmarkProjectGenerator
{
    Task<string> GenerateUnitBenchmarkProjectAsync(string solutionPath, List<ProjectInfo> projects);
    Task<string> GenerateE2EBenchmarkProjectAsync(string solutionPath, List<ProjectInfo> projects);
}
