using Benchmark.Core.Models;
using System.Text;

namespace Benchmark.Core.Services;

public class ReportGenerator : IReportGenerator
{
    public async Task GenerateMarkdownReportAsync(List<BenchmarkSummary> summaries, string outputPath)
    {
        var markdown = new StringBuilder();

        // Title and header
        markdown.AppendLine("# Benchmark Results Report");
        markdown.AppendLine();
        markdown.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        markdown.AppendLine();

        // Executive summary
        markdown.AppendLine("## Executive Summary");
        markdown.AppendLine();
        
        var totalBenchmarks = summaries.SelectMany(s => s.Results).Count();
        var successfulRuns = summaries.Count(s => s.Success);
        var failedRuns = summaries.Count(s => !s.Success);
        
        markdown.AppendLine($"- **Total Benchmark Projects:** {summaries.Count}");
        markdown.AppendLine($"- **Successful Runs:** {successfulRuns}");
        markdown.AppendLine($"- **Failed Runs:** {failedRuns}");
        markdown.AppendLine($"- **Total Benchmarks Executed:** {totalBenchmarks}");
        markdown.AppendLine();

        // Performance overview
        if (totalBenchmarks > 0)
        {
            var allResults = summaries.SelectMany(s => s.Results).ToList();
            var avgThroughput = allResults.Average(r => 1_000_000_000.0 / r.MeanNanoseconds); // ops/sec
            var avgMemory = allResults.Average(r => r.AllocatedBytes);

            markdown.AppendLine("## Performance Overview");
            markdown.AppendLine();
            markdown.AppendLine($"- **Average Throughput:** {avgThroughput:N0} operations/second");
            markdown.AppendLine($"- **Average Memory Allocation:** {avgMemory / 1024.0:N2} KB per operation");
            markdown.AppendLine();
        }

        // Detailed results for each project
        foreach (var summary in summaries.Where(s => s.Success))
        {
            markdown.AppendLine($"## {summary.ProjectName}");
            markdown.AppendLine();
            markdown.AppendLine($"**Execution Time:** {summary.ExecutionTime:yyyy-MM-dd HH:mm:ss} UTC");
            markdown.AppendLine();

            if (summary.Results.Any())
            {
                // Performance metrics table
                markdown.AppendLine("### Performance Metrics");
                markdown.AppendLine();
                markdown.AppendLine("| Benchmark | Mean Time | Std Dev | Throughput (ops/sec) | Memory Allocated |");
                markdown.AppendLine("|-----------|-----------|---------|---------------------|------------------|");

                foreach (var result in summary.Results.OrderBy(r => r.MeanNanoseconds))
                {
                    var throughput = 1_000_000_000.0 / result.MeanNanoseconds;
                    var meanTime = FormatTime(result.MeanNanoseconds);
                    var stdDev = FormatTime(result.StdDevNanoseconds);
                    var memory = FormatMemory(result.AllocatedBytes);

                    markdown.AppendLine($"| {result.Method} | {meanTime} | {stdDev} | {throughput:N0} | {memory} |");
                }

                markdown.AppendLine();

                // Quality analysis
                markdown.AppendLine("### Quality Analysis");
                markdown.AppendLine();

                var orderedResults = summary.Results.OrderBy(r => r.MeanNanoseconds).ToList();
                var fastestBenchmark = orderedResults.First();
                var slowestBenchmark = orderedResults.Last();
                var mostMemoryIntensive = summary.Results.OrderByDescending(r => r.AllocatedBytes).First();

                markdown.AppendLine($"**Fastest Operation:**");
                markdown.AppendLine($"- `{fastestBenchmark.Method}`: {FormatTime(fastestBenchmark.MeanNanoseconds)}");
                markdown.AppendLine();

                markdown.AppendLine($"**Slowest Operation:**");
                markdown.AppendLine($"- `{slowestBenchmark.Method}`: {FormatTime(slowestBenchmark.MeanNanoseconds)}");
                markdown.AppendLine();

                markdown.AppendLine($"**Most Memory Intensive:**");
                markdown.AppendLine($"- `{mostMemoryIntensive.Method}`: {FormatMemory(mostMemoryIntensive.AllocatedBytes)}");
                markdown.AppendLine();

                // Performance limits and recommendations
                markdown.AppendLine("### Performance Limits & Recommendations");
                markdown.AppendLine();

                var highMemoryOps = summary.Results.Where(r => r.AllocatedBytes > 1024 * 100).ToList(); // > 100KB
                if (highMemoryOps.Any())
                {
                    markdown.AppendLine("**⚠️ High Memory Allocation Detected:**");
                    foreach (var op in highMemoryOps)
                    {
                        markdown.AppendLine($"- `{op.Method}`: {FormatMemory(op.AllocatedBytes)} per operation");
                    }
                    markdown.AppendLine();
                }

                var slowOps = summary.Results.Where(r => r.MeanMilliseconds > 100).ToList(); // > 100ms
                if (slowOps.Any())
                {
                    markdown.AppendLine("**⚠️ Slow Operations Detected:**");
                    foreach (var op in slowOps)
                    {
                        markdown.AppendLine($"- `{op.Method}`: {FormatTime(op.MeanNanoseconds)}");
                    }
                    markdown.AppendLine();
                }

                if (!highMemoryOps.Any() && !slowOps.Any())
                {
                    markdown.AppendLine("✅ All operations are performing within acceptable limits.");
                    markdown.AppendLine();
                }
            }
            else
            {
                markdown.AppendLine("*No benchmark results available for this project.*");
                markdown.AppendLine();
            }
        }

        // Failed runs section
        if (failedRuns > 0)
        {
            markdown.AppendLine("## Failed Runs");
            markdown.AppendLine();

            foreach (var summary in summaries.Where(s => !s.Success))
            {
                markdown.AppendLine($"### {summary.ProjectName}");
                markdown.AppendLine();
                markdown.AppendLine($"**Error:** {summary.ErrorMessage}");
                markdown.AppendLine();
            }
        }

        // Appendix with interpretation guide
        markdown.AppendLine("---");
        markdown.AppendLine();
        markdown.AppendLine("## Interpretation Guide");
        markdown.AppendLine();
        markdown.AppendLine("### Metrics Explained");
        markdown.AppendLine();
        markdown.AppendLine("- **Mean Time:** Average execution time per operation");
        markdown.AppendLine("- **Std Dev:** Standard deviation indicating consistency");
        markdown.AppendLine("- **Throughput:** Number of operations that can be performed per second");
        markdown.AppendLine("- **Memory Allocated:** Bytes allocated on the heap per operation");
        markdown.AppendLine();
        markdown.AppendLine("### Performance Categories");
        markdown.AppendLine();
        markdown.AppendLine("- **Fast:** < 1ms");
        markdown.AppendLine("- **Moderate:** 1-100ms");
        markdown.AppendLine("- **Slow:** > 100ms");
        markdown.AppendLine();
        markdown.AppendLine("### Memory Categories");
        markdown.AppendLine();
        markdown.AppendLine("- **Low:** < 1KB");
        markdown.AppendLine("- **Moderate:** 1-100KB");
        markdown.AppendLine("- **High:** > 100KB");

        await File.WriteAllTextAsync(outputPath, markdown.ToString());
    }

    private string FormatTime(double nanoseconds)
    {
        if (nanoseconds < 1_000)
            return $"{nanoseconds:F2} ns";
        if (nanoseconds < 1_000_000)
            return $"{nanoseconds / 1_000:F2} μs";
        if (nanoseconds < 1_000_000_000)
            return $"{nanoseconds / 1_000_000:F2} ms";
        return $"{nanoseconds / 1_000_000_000:F2} s";
    }

    private string FormatMemory(double bytes)
    {
        if (bytes == 0)
            return "0 B";
        if (bytes < 1024)
            return $"{bytes:F0} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024:F2} KB";
        return $"{bytes / (1024 * 1024):F2} MB";
    }
}
