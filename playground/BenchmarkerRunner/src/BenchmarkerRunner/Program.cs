using Benchmark.Core.Services;
using BenchmarkerRunner.Configuration;
using BenchmarkerRunner.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BenchmarkerRunner;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var repoRoot = FindRepositoryRoot();
        var config = LoadConfiguration();
        var services = BuildServiceProvider(config, repoRoot);

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();
        var options = services.GetRequiredService<IOptions<RunnerOptions>>().Value;

        logger.LogInformation("BenchmarkerRunner starting");
        logger.LogInformation("Repository root: {Root}", repoRoot);
        logger.LogInformation("Solutions directory: {Dir}", options.SolutionsDirectory);
        logger.LogInformation("Artifacts directory: {Dir}", options.ArtifactsDirectory);

        // Set up graceful Ctrl+C cancellation
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            logger.LogWarning("Cancellation requested, finishing current operation...");
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            var orchestrator = services.GetRequiredService<IBenchmarkOrchestrator>();
            var successCount = await orchestrator.RunAllAsync(cts.Token);

            if (successCount > 0)
            {
                logger.LogInformation("Completed successfully with {Count} solutions benchmarked", successCount);
                return 0;
            }
            else
            {
                logger.LogError("All solutions failed to benchmark");
                return 1;
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Benchmarking was cancelled by user");
            return 2;
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;

        // Walk up directory tree looking for Benchmark.slnx
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "Benchmark.slnx")))
            {
                return directory;
            }
            directory = Path.GetDirectoryName(directory);
        }

        // Fallback: also try from current working directory
        directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "Benchmark.slnx")))
            {
                return directory;
            }
            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException(
            "Cannot find repository root (no Benchmark.slnx found in parent directories)");
    }

    private static IConfiguration LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static ServiceProvider BuildServiceProvider(IConfiguration config, string repoRoot)
    {
        var services = new ServiceCollection();

        services.AddSingleton(config);

        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Bind Runner options with auto-resolved defaults
        services.Configure<RunnerOptions>(options =>
        {
            config.GetSection("Runner").Bind(options);

            if (string.IsNullOrEmpty(options.SolutionsDirectory))
                options.SolutionsDirectory = Path.Combine(repoRoot, "playground", "solutions");

            if (string.IsNullOrEmpty(options.ArtifactsDirectory))
                options.ArtifactsDirectory = Path.Combine(repoRoot, "artifacts");
        });

        // Register Benchmark.Core services (same as Benchmark.Cli)
        services.AddSingleton<ISolutionAnalyzer, SolutionAnalyzer>();
        services.AddSingleton<IBenchmarkProjectGenerator, BenchmarkProjectGenerator>();
        services.AddSingleton<IBenchmarkRunner, Benchmark.Core.Services.BenchmarkRunner>();
        services.AddSingleton<IReportGenerator, ReportGenerator>();

        // Register BenchmarkerRunner services
        services.AddSingleton<ISolutionCopier, SolutionCopier>();
        services.AddSingleton<IBenchmarkOrchestrator, BenchmarkOrchestrator>();

        return services.BuildServiceProvider();
    }
}
