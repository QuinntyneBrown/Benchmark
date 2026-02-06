using Benchmark.Core.Models;
using Benchmark.Core.Services;
using BenchmarkerRunner.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BenchmarkerRunner.Services;

public class BenchmarkOrchestrator : IBenchmarkOrchestrator
{
    private readonly ISolutionCopier _solutionCopier;
    private readonly ISolutionAnalyzer _solutionAnalyzer;
    private readonly IBenchmarkProjectGenerator _projectGenerator;
    private readonly IBenchmarkRunner _benchmarkRunner;
    private readonly IReportGenerator _reportGenerator;
    private readonly RunnerOptions _options;
    private readonly ILogger<BenchmarkOrchestrator> _logger;

    public BenchmarkOrchestrator(
        ISolutionCopier solutionCopier,
        ISolutionAnalyzer solutionAnalyzer,
        IBenchmarkProjectGenerator projectGenerator,
        IBenchmarkRunner benchmarkRunner,
        IReportGenerator reportGenerator,
        IOptions<RunnerOptions> options,
        ILogger<BenchmarkOrchestrator> logger)
    {
        _solutionCopier = solutionCopier;
        _solutionAnalyzer = solutionAnalyzer;
        _projectGenerator = projectGenerator;
        _benchmarkRunner = benchmarkRunner;
        _reportGenerator = reportGenerator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> RunAllAsync(CancellationToken cancellationToken)
    {
        var solutions = _solutionCopier.DiscoverSolutions(_options.SolutionsDirectory);

        if (solutions.Count == 0)
        {
            _logger.LogWarning("No solutions found in {Directory}", _options.SolutionsDirectory);
            return 0;
        }

        _logger.LogInformation("Found {Count} solutions to benchmark", solutions.Count);

        var successCount = 0;
        var allSummaries = new List<BenchmarkSummary>();

        foreach (var solutionPath in solutions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var solutionName = Path.GetFileNameWithoutExtension(
                Path.GetDirectoryName(solutionPath) ?? solutionPath);

            try
            {
                _logger.LogInformation("===================================");
                _logger.LogInformation("Processing solution: {Solution}", solutionName);
                _logger.LogInformation("===================================");

                var summaries = await ProcessSolutionAsync(solutionPath, cancellationToken);
                allSummaries.AddRange(summaries);
                successCount++;

                _logger.LogInformation("Solution {Solution} benchmarked successfully", solutionName);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Benchmarking cancelled during {Solution}", solutionName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to benchmark solution {Solution}, continuing to next", solutionName);
            }
        }

        // Generate aggregate reports from all collected summaries
        if (allSummaries.Count > 0)
        {
            var aggregateReportPath = Path.Combine(_options.ArtifactsDirectory, "AggregateReport.md");
            _logger.LogInformation("Generating aggregate report at {Path}", aggregateReportPath);
            await _reportGenerator.GenerateMarkdownReportAsync(allSummaries, aggregateReportPath);

            var productOwnerReportPath = Path.Combine(_options.ArtifactsDirectory, "ProductOwnerReport.md");
            _logger.LogInformation("Generating product owner report at {Path}", productOwnerReportPath);
            await _reportGenerator.GenerateProductOwnerReportAsync(allSummaries, productOwnerReportPath);
        }

        _logger.LogInformation("===================================");
        _logger.LogInformation("Benchmarking complete: {Success}/{Total} solutions succeeded",
            successCount, solutions.Count);
        _logger.LogInformation("===================================");

        return successCount;
    }

    private async Task<List<BenchmarkSummary>> ProcessSolutionAsync(
        string solutionPath, CancellationToken cancellationToken)
    {
        // Step 1: Copy solution to artifacts
        _logger.LogInformation("Step 1/5: Copying solution to artifacts...");
        var copiedSlnxPath = _solutionCopier.CopySolutionToArtifacts(solutionPath, _options.ArtifactsDirectory);
        var artifactDir = Path.GetDirectoryName(copiedSlnxPath)!;

        // Step 2: Analyze solution
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Step 2/5: Analyzing solution...");
        var projects = await _solutionAnalyzer.AnalyzeSolutionAsync(copiedSlnxPath);
        _logger.LogInformation("Discovered {Count} projects with public APIs", projects.Count);

        // Step 3: Generate benchmark projects
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Step 3/5: Generating benchmark projects...");
        var unitBenchmarkPath = await _projectGenerator.GenerateUnitBenchmarkProjectAsync(copiedSlnxPath, projects);
        _logger.LogInformation("Unit benchmarks generated at: {Path}", unitBenchmarkPath);

        var e2eBenchmarkPath = await _projectGenerator.GenerateE2EBenchmarkProjectAsync(copiedSlnxPath, projects);
        _logger.LogInformation("E2E benchmarks generated at: {Path}", e2eBenchmarkPath);

        // Step 4: Run benchmarks
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Step 4/5: Running benchmarks (this may take several minutes)...");
        var summaries = new List<BenchmarkSummary>();

        _logger.LogInformation("Running unit benchmarks...");
        var unitSummary = await _benchmarkRunner.RunBenchmarksAsync(unitBenchmarkPath);
        summaries.Add(unitSummary);
        if (unitSummary.Success)
            _logger.LogInformation("Unit benchmarks completed with {Count} results", unitSummary.Results.Count);
        else
            _logger.LogWarning("Unit benchmarks failed: {Error}", unitSummary.ErrorMessage);

        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Running E2E benchmarks...");
        var e2eSummary = await _benchmarkRunner.RunBenchmarksAsync(e2eBenchmarkPath);
        summaries.Add(e2eSummary);
        if (e2eSummary.Success)
            _logger.LogInformation("E2E benchmarks completed with {Count} results", e2eSummary.Results.Count);
        else
            _logger.LogWarning("E2E benchmarks failed: {Error}", e2eSummary.ErrorMessage);

        // Step 5: Generate per-solution reports
        _logger.LogInformation("Step 5/5: Generating solution reports...");
        var reportPath = Path.Combine(artifactDir, "BenchmarkReport.md");
        await _reportGenerator.GenerateMarkdownReportAsync(summaries, reportPath);
        _logger.LogInformation("Technical report generated at: {Path}", reportPath);

        var poReportPath = Path.Combine(artifactDir, "ProductOwnerReport.md");
        await _reportGenerator.GenerateProductOwnerReportAsync(summaries, poReportPath);
        _logger.LogInformation("Product owner report generated at: {Path}", poReportPath);

        return summaries;
    }
}
