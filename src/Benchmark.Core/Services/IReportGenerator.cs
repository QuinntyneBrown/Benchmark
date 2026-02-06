using Benchmark.Core.Models;

namespace Benchmark.Core.Services;

public interface IReportGenerator
{
    Task GenerateMarkdownReportAsync(List<BenchmarkSummary> summaries, string outputPath);
    Task GenerateProductOwnerReportAsync(List<BenchmarkSummary> summaries, string outputPath);
}
