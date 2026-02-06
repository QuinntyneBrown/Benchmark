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

    public async Task GenerateProductOwnerReportAsync(List<BenchmarkSummary> summaries, string outputPath)
    {
        var md = new StringBuilder();

        md.AppendLine("# Performance Summary for Product Owners");
        md.AppendLine();
        md.AppendLine($"**Report Date:** {DateTime.UtcNow:MMMM d, yyyy}");
        md.AppendLine();

        var successfulSummaries = summaries.Where(s => s.Success).ToList();
        var failedSummaries = summaries.Where(s => !s.Success).ToList();

        // Separate E2E (SignalR message flow) results from unit-level results
        var signalrResults = successfulSummaries
            .SelectMany(s => s.Results.Select(r => (Summary: s, Result: r)))
            .Where(x => x.Result.Method == "MeasureSignalRMessageFlow")
            .ToList();

        var unitResults = successfulSummaries
            .SelectMany(s => s.Results.Select(r => (Summary: s, Result: r)))
            .Where(x => x.Result.Method != "MeasureSignalRMessageFlow"
                     && x.Result.Method != "MeasureRootEndpoint"
                     && x.Result.Method != "MeasureConsoleExecution")
            .ToList();

        // ── Section 1: System Health at a Glance ──
        md.AppendLine("## System Health at a Glance");
        md.AppendLine();

        var totalSystems = summaries.Count;
        var healthySystems = successfulSummaries.Count;

        if (failedSummaries.Count == 0)
        {
            md.AppendLine($"All **{healthySystems}** systems completed testing successfully.");
        }
        else
        {
            md.AppendLine($"**{healthySystems}** of **{totalSystems}** systems completed testing successfully. "
                + $"**{failedSummaries.Count}** system(s) failed to complete (see below).");
        }
        md.AppendLine();

        // ── Section 2: Message Processing Capacity ──
        if (signalrResults.Count > 0)
        {
            md.AppendLine("## Message Processing Capacity");
            md.AppendLine();
            md.AppendLine("Each system receives messages, processes them, and pushes results to connected clients via SignalR. "
                + "The table below shows how quickly each system completes one full message cycle "
                + "(receive, process, and deliver).");
            md.AppendLine();

            md.AppendLine("| System | Time per Message | Est. Messages per Second | Memory per Message | Verdict |");
            md.AppendLine("|--------|-----------------|--------------------------|-------------------|---------|");

            foreach (var entry in signalrResults.OrderBy(x => x.Result.MeanNanoseconds))
            {
                var projectLabel = FormatProjectLabel(entry.Summary.ProjectName);
                var timePerMsg = FormatTimeFriendly(entry.Result.MeanNanoseconds);
                var msgsPerSec = 1_000_000_000.0 / entry.Result.MeanNanoseconds;
                var memPerMsg = FormatMemory(entry.Result.AllocatedBytes);
                var verdict = GetCapacityVerdict(entry.Result.MeanMilliseconds, entry.Result.AllocatedBytes);

                md.AppendLine($"| {projectLabel} | {timePerMsg} | ~{msgsPerSec:N0} | {memPerMsg} | {verdict} |");
            }

            md.AppendLine();

            // Estimate sustained throughput context
            var fastestSignalR = signalrResults.OrderBy(x => x.Result.MeanNanoseconds).First();
            var slowestSignalR = signalrResults.OrderByDescending(x => x.Result.MeanNanoseconds).First();

            md.AppendLine("### What This Means");
            md.AppendLine();
            md.AppendLine($"- **Best performer** ({FormatProjectLabel(fastestSignalR.Summary.ProjectName)}): "
                + $"Can handle approximately **{1_000_000_000.0 / fastestSignalR.Result.MeanNanoseconds:N0} messages per second** "
                + $"using **{FormatMemory(fastestSignalR.Result.AllocatedBytes)}** of memory each.");
            md.AppendLine($"- **Needs attention** ({FormatProjectLabel(slowestSignalR.Summary.ProjectName)}): "
                + $"Handles approximately **{1_000_000_000.0 / slowestSignalR.Result.MeanNanoseconds:N0} messages per second** "
                + $"using **{FormatMemory(slowestSignalR.Result.AllocatedBytes)}** of memory each.");
            md.AppendLine();

            // Crash risk assessment
            md.AppendLine("### Crash Risk Assessment");
            md.AppendLine();

            var highMemorySignalR = signalrResults
                .Where(x => x.Result.AllocatedBytes > 100 * 1024)
                .OrderByDescending(x => x.Result.AllocatedBytes)
                .ToList();

            var slowSignalR = signalrResults
                .Where(x => x.Result.MeanMilliseconds > 100)
                .OrderByDescending(x => x.Result.MeanNanoseconds)
                .ToList();

            if (highMemorySignalR.Count == 0 && slowSignalR.Count == 0)
            {
                md.AppendLine("No immediate crash risks detected. All systems are processing messages within safe limits.");
            }
            else
            {
                if (highMemorySignalR.Count > 0)
                {
                    md.AppendLine("**Memory Pressure (risk of out-of-memory crashes under load):**");
                    md.AppendLine();
                    foreach (var entry in highMemorySignalR)
                    {
                        md.AppendLine($"- **{FormatProjectLabel(entry.Summary.ProjectName)}** uses "
                            + $"**{FormatMemory(entry.Result.AllocatedBytes)}** per message. "
                            + $"At {1_000_000_000.0 / entry.Result.MeanNanoseconds:N0} messages/sec, "
                            + $"this means ~**{entry.Result.AllocatedBytes * (1_000_000_000.0 / entry.Result.MeanNanoseconds) / (1024 * 1024):N0} MB/sec** "
                            + "of memory allocation, which the garbage collector must reclaim continuously.");
                    }
                    md.AppendLine();
                }

                if (slowSignalR.Count > 0)
                {
                    md.AppendLine("**Slow Message Delivery (risk of message backlog and timeouts):**");
                    md.AppendLine();
                    foreach (var entry in slowSignalR)
                    {
                        md.AppendLine($"- **{FormatProjectLabel(entry.Summary.ProjectName)}** takes "
                            + $"**{FormatTimeFriendly(entry.Result.MeanNanoseconds)}** per message. "
                            + "If incoming messages arrive faster than they can be processed, "
                            + "the queue will grow until the system runs out of memory or starts dropping messages.");
                    }
                    md.AppendLine();
                }
            }
        }

        // ── Section 3: Internal Processing Performance ──
        if (unitResults.Count > 0)
        {
            md.AppendLine("## Internal Processing Performance");
            md.AppendLine();
            md.AppendLine("These are the individual processing steps that run inside each system. "
                + "Slow or memory-heavy steps are the most likely places to find performance improvements.");
            md.AppendLine();

            // Group by project
            var groupedByProject = unitResults
                .GroupBy(x => x.Summary.ProjectName)
                .OrderBy(g => g.Key);

            foreach (var projectGroup in groupedByProject)
            {
                var projectLabel = FormatProjectLabel(projectGroup.Key);
                md.AppendLine($"### {projectLabel}");
                md.AppendLine();
                md.AppendLine("| Operation | Speed | Memory Used | Status |");
                md.AppendLine("|-----------|-------|-------------|--------|");

                foreach (var entry in projectGroup.OrderByDescending(x => x.Result.MeanNanoseconds))
                {
                    var opName = FormatMethodName(entry.Result.Method);
                    var speed = FormatTimeFriendly(entry.Result.MeanNanoseconds);
                    var mem = FormatMemory(entry.Result.AllocatedBytes);
                    var status = GetOperationStatus(entry.Result.MeanMilliseconds, entry.Result.AllocatedBytes);

                    md.AppendLine($"| {opName} | {speed} | {mem} | {status} |");
                }

                md.AppendLine();
            }
        }

        // ── Section 4: Where to Focus Development Effort ──
        md.AppendLine("## Where to Focus Development Effort");
        md.AppendLine();
        md.AppendLine("The following list ranks areas by their potential impact on overall system performance. "
            + "Items at the top will give the most improvement if optimized.");
        md.AppendLine();

        var allActionableResults = unitResults
            .Concat(signalrResults)
            .OrderByDescending(x => x.Result.MeanNanoseconds * (1 + x.Result.AllocatedBytes / 1024.0))
            .ToList();

        var rank = 1;
        var seenProjects = new HashSet<string>();

        foreach (var entry in allActionableResults)
        {
            if (rank > 10) break;

            var projectLabel = FormatProjectLabel(entry.Summary.ProjectName);
            var opName = entry.Result.Method == "MeasureSignalRMessageFlow"
                ? "End-to-end message delivery"
                : FormatMethodName(entry.Result.Method);
            var speed = FormatTimeFriendly(entry.Result.MeanNanoseconds);
            var mem = FormatMemory(entry.Result.AllocatedBytes);

            var reason = new List<string>();
            if (entry.Result.MeanMilliseconds > 100)
                reason.Add("slow processing");
            else if (entry.Result.MeanMilliseconds > 10)
                reason.Add("moderate processing time");
            if (entry.Result.AllocatedBytes > 100 * 1024)
                reason.Add("high memory usage");
            else if (entry.Result.AllocatedBytes > 10 * 1024)
                reason.Add("moderate memory usage");
            if (reason.Count == 0)
                reason.Add("optimization opportunity");

            md.AppendLine($"**{rank}.** **{projectLabel}** — {opName}");
            md.AppendLine($"   - Takes {speed}, uses {mem} per operation");
            md.AppendLine($"   - Reason: {string.Join(", ", reason)}");
            md.AppendLine();

            rank++;
        }

        if (allActionableResults.Count == 0)
        {
            md.AppendLine("No benchmark data available to generate recommendations.");
            md.AppendLine();
        }

        // ── Section 5: Systems That Failed Testing ──
        if (failedSummaries.Count > 0)
        {
            md.AppendLine("## Systems That Failed Testing");
            md.AppendLine();
            md.AppendLine("The following systems could not complete performance testing. "
                + "These should be investigated first, as a system that cannot complete testing may also fail in production.");
            md.AppendLine();

            foreach (var failed in failedSummaries)
            {
                var label = FormatProjectLabel(failed.ProjectName);
                md.AppendLine($"- **{label}**: Testing could not complete. Developer investigation required.");
            }
            md.AppendLine();
        }

        // ── Glossary ──
        md.AppendLine("---");
        md.AppendLine();
        md.AppendLine("## How to Read This Report");
        md.AppendLine();
        md.AppendLine("- **Time per Message**: How long it takes for one message to travel through the entire system "
            + "(received, processed, and delivered to the client). Lower is better.");
        md.AppendLine("- **Messages per Second**: How many messages the system can handle each second. Higher is better.");
        md.AppendLine("- **Memory per Message**: How much temporary memory is used to process a single message. "
            + "Lower is better. High memory usage under heavy load can cause crashes.");
        md.AppendLine("- **Verdict/Status meanings**:");
        md.AppendLine("  - **Healthy**: Operating well within safe limits");
        md.AppendLine("  - **Acceptable**: Working fine but has room for improvement");
        md.AppendLine("  - **Needs Investigation**: May cause problems under heavy load");
        md.AppendLine("  - **At Risk**: Likely to cause issues in production and should be prioritized");

        await File.WriteAllTextAsync(outputPath, md.ToString());
    }

    private static string FormatProjectLabel(string projectName)
    {
        // "Apollo.SignalProcessor.E2EBenchmarks" → "Apollo Signal Processor"
        // "Apollo.SignalProcessor.UnitBenchmarks" → "Apollo Signal Processor"
        var name = projectName
            .Replace(".E2EBenchmarks", "")
            .Replace(".UnitBenchmarks", "");

        // Split on dots and insert spaces before capitals within each segment
        var segments = name.Split('.');
        return string.Join(" ", segments);
    }

    private static string FormatMethodName(string method)
    {
        // "Measure_ProcessSignals" → "Process Signals"
        var cleaned = method
            .Replace("Measure_", "")
            .Replace("Measure", "");

        // Insert spaces before uppercase letters
        var result = new StringBuilder();
        foreach (var ch in cleaned)
        {
            if (char.IsUpper(ch) && result.Length > 0 && result[result.Length - 1] != ' ')
                result.Append(' ');
            result.Append(ch);
        }

        return result.ToString().Trim();
    }

    private static string FormatTimeFriendly(double nanoseconds)
    {
        if (nanoseconds < 1_000)
            return "under 1 microsecond";
        if (nanoseconds < 1_000_000)
            return $"{nanoseconds / 1_000:F1} microseconds";
        if (nanoseconds < 1_000_000_000)
            return $"{nanoseconds / 1_000_000:F1} ms";
        return $"{nanoseconds / 1_000_000_000:F2} seconds";
    }

    private static string GetCapacityVerdict(double meanMs, double allocatedBytes)
    {
        if (meanMs > 100 || allocatedBytes > 500 * 1024)
            return "At Risk";
        if (meanMs > 10 || allocatedBytes > 100 * 1024)
            return "Needs Investigation";
        if (meanMs > 1 || allocatedBytes > 10 * 1024)
            return "Acceptable";
        return "Healthy";
    }

    private static string GetOperationStatus(double meanMs, double allocatedBytes)
    {
        if (meanMs > 100 || allocatedBytes > 500 * 1024)
            return "At Risk";
        if (meanMs > 10 || allocatedBytes > 100 * 1024)
            return "Needs Investigation";
        if (meanMs > 1 || allocatedBytes > 10 * 1024)
            return "Acceptable";
        return "Healthy";
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
