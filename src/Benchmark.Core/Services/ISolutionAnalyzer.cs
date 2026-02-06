using Benchmark.Core.Models;

namespace Benchmark.Core.Services;

public interface ISolutionAnalyzer
{
    Task<List<ProjectInfo>> AnalyzeSolutionAsync(string solutionPath);
}
