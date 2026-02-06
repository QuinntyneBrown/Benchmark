using Benchmark.Core.Services;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace Benchmark.Cli.Commands;

public class RunAndReportCommand : Command
{
    public RunAndReportCommand() : base("run-and-report", "Generate benchmarks, run them, and create comprehensive markdown reports")
    {
        var pathArg = new Argument<string>(
            name: "solution-path",
            description: "Path to the .NET solution file (.sln or .slnx)");

        var outputOption = new Option<string>(
            name: "--output",
            description: "Output path for the markdown report (defaults to BenchmarkReport.md in solution directory)",
            getDefaultValue: () => string.Empty);

        AddArgument(pathArg);
        AddOption(outputOption);

        this.SetHandler(async (solutionFilePath, outputPath) =>
        {
            await ExecuteRunAndReportWorkflow(solutionFilePath, outputPath);
        }, pathArg, outputOption);
    }

    private async Task<int> ExecuteRunAndReportWorkflow(string solutionFilePath, string outputPath)
    {
        var container = Program.ServiceProvider;
        var logFactory = container.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
        var commandLogger = logFactory?.CreateLogger<RunAndReportCommand>();
        
        var solutionAnalyzer = container.GetService(typeof(ISolutionAnalyzer)) as ISolutionAnalyzer;
        var projectGenerator = container.GetService(typeof(IBenchmarkProjectGenerator)) as IBenchmarkProjectGenerator;
        var benchmarkRunner = container.GetService(typeof(IBenchmarkRunner)) as IBenchmarkRunner;
        var reportGenerator = container.GetService(typeof(IReportGenerator)) as IReportGenerator;

        if (solutionAnalyzer == null || projectGenerator == null || benchmarkRunner == null || reportGenerator == null)
        {
            commandLogger?.LogError("Unable to resolve required dependencies");
            return 1;
        }

        try
        {
            // Step 1: Generate benchmarks
            commandLogger?.LogInformation("Step 1/4: Generating benchmark projects...");
            
            var projectsData = await solutionAnalyzer.AnalyzeSolutionAsync(solutionFilePath);
            commandLogger?.LogInformation("Discovered {ProjectCount} projects with public APIs", projectsData.Count);

            var unitBenchmarkLocation = await projectGenerator.GenerateUnitBenchmarkProjectAsync(solutionFilePath, projectsData);
            commandLogger?.LogInformation("Unit benchmarks generated at: {Location}", unitBenchmarkLocation);

            var e2eBenchmarkLocation = await projectGenerator.GenerateE2EBenchmarkProjectAsync(solutionFilePath, projectsData);
            commandLogger?.LogInformation("E2E benchmarks generated at: {Location}", e2eBenchmarkLocation);

            // Step 2: Run benchmarks and collect results
            commandLogger?.LogInformation("Step 2/4: Running benchmarks (this may take several minutes)...");
            
            var summaries = new List<Benchmark.Core.Models.BenchmarkSummary>();

            commandLogger?.LogInformation("Running unit benchmarks...");
            var unitSummary = await benchmarkRunner.RunBenchmarksAsync(unitBenchmarkLocation);
            summaries.Add(unitSummary);
            
            if (unitSummary.Success)
            {
                commandLogger?.LogInformation("Unit benchmarks completed successfully with {Count} results", unitSummary.Results.Count);
            }
            else
            {
                commandLogger?.LogWarning("Unit benchmarks failed: {Error}", unitSummary.ErrorMessage);
            }

            commandLogger?.LogInformation("Running E2E benchmarks...");
            var e2eSummary = await benchmarkRunner.RunBenchmarksAsync(e2eBenchmarkLocation);
            summaries.Add(e2eSummary);
            
            if (e2eSummary.Success)
            {
                commandLogger?.LogInformation("E2E benchmarks completed successfully with {Count} results", e2eSummary.Results.Count);
            }
            else
            {
                commandLogger?.LogWarning("E2E benchmarks failed: {Error}", e2eSummary.ErrorMessage);
            }

            // Step 3: Generate report
            commandLogger?.LogInformation("Step 3/4: Generating markdown report...");
            
            if (string.IsNullOrEmpty(outputPath))
            {
                var solutionDir = Path.GetDirectoryName(solutionFilePath) ?? Directory.GetCurrentDirectory();
                outputPath = Path.Combine(solutionDir, "BenchmarkReport.md");
            }

            await reportGenerator.GenerateMarkdownReportAsync(summaries, outputPath);
            commandLogger?.LogInformation("Technical report generated at: {Path}", outputPath);

            var productOwnerReportPath = Path.Combine(
                Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory(),
                "ProductOwnerReport.md");
            await reportGenerator.GenerateProductOwnerReportAsync(summaries, productOwnerReportPath);
            commandLogger?.LogInformation("Product owner report generated at: {Path}", productOwnerReportPath);

            // Step 4: Summary
            commandLogger?.LogInformation("Step 4/4: Workflow completed!");
            commandLogger?.LogInformation("===================================");
            commandLogger?.LogInformation("Benchmark execution summary:");
            commandLogger?.LogInformation("  - Total benchmarks run: {Count}", summaries.SelectMany(s => s.Results).Count());
            commandLogger?.LogInformation("  - Technical report: {Path}", outputPath);
            commandLogger?.LogInformation("  - Product owner report: {Path}", productOwnerReportPath);
            commandLogger?.LogInformation("===================================");

            return 0;
        }
        catch (Exception error)
        {
            commandLogger?.LogError(error, "Failed to complete run-and-report workflow");
            return 1;
        }
    }
}
