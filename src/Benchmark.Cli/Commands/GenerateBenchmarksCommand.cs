using Benchmark.Core.Services;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace Benchmark.Cli.Commands;

public class GenerateBenchmarksCommand : Command
{
    public GenerateBenchmarksCommand() : base("generate", "Generate benchmark projects for a .NET solution")
    {
        var pathArg = new Argument<string>(
            name: "solution-path",
            description: "Path to the .NET solution file (.sln)");

        var verboseFlag = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose logging");

        AddArgument(pathArg);
        AddOption(verboseFlag);

        this.SetHandler(async (solutionFilePath, enableVerbose) =>
        {
            await ExecuteGenerationWorkflow(solutionFilePath, enableVerbose);
        }, pathArg, verboseFlag);
    }

    private async Task<int> ExecuteGenerationWorkflow(string solutionFilePath, bool verboseMode)
    {
        var container = Program.ServiceProvider;
        var logFactory = container.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
        var commandLogger = logFactory?.CreateLogger<GenerateBenchmarksCommand>();
        
        var solutionAnalyzer = container.GetService(typeof(ISolutionAnalyzer)) as ISolutionAnalyzer;
        var projectGenerator = container.GetService(typeof(IBenchmarkProjectGenerator)) as IBenchmarkProjectGenerator;

        if (solutionAnalyzer == null || projectGenerator == null)
        {
            commandLogger?.LogError("Unable to resolve required dependencies");
            return 1;
        }

        try
        {
            commandLogger?.LogInformation("Starting analysis of solution: {Path}", solutionFilePath);
            
            var projectsData = await solutionAnalyzer.AnalyzeSolutionAsync(solutionFilePath);
            
            commandLogger?.LogInformation("Discovered {ProjectCount} projects with public APIs", projectsData.Count);

            commandLogger?.LogInformation("Creating unit benchmark project...");
            var unitBenchmarkLocation = await projectGenerator.GenerateUnitBenchmarkProjectAsync(solutionFilePath, projectsData);
            commandLogger?.LogInformation("Unit benchmarks generated at: {Location}", unitBenchmarkLocation);

            commandLogger?.LogInformation("Creating E2E benchmark project...");
            var e2eBenchmarkLocation = await projectGenerator.GenerateE2EBenchmarkProjectAsync(solutionFilePath, projectsData);
            commandLogger?.LogInformation("E2E benchmarks generated at: {Location}", e2eBenchmarkLocation);

            commandLogger?.LogInformation("Benchmark project generation completed successfully!");
            return 0;
        }
        catch (Exception error)
        {
            commandLogger?.LogError(error, "Failed to generate benchmarks");
            return 1;
        }
    }
}
