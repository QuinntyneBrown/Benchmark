using Benchmark.Core.Models;
using System.Diagnostics;
using System.Text.Json;

namespace Benchmark.Core.Services;

public class BenchmarkRunner : IBenchmarkRunner
{
    public async Task<BenchmarkSummary> RunBenchmarksAsync(string projectPath)
    {
        var summary = new BenchmarkSummary
        {
            ProjectName = Path.GetFileName(projectPath),
            ExecutionTime = DateTime.UtcNow
        };

        try
        {
            // Run dotnet run -c Release in the project directory
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run -c Release -- --exporters json",
                WorkingDirectory = projectPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start benchmark process");
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var errorOutput = await process.StandardError.ReadToEndAsync();
                summary.Success = false;
                summary.ErrorMessage = $"Benchmark execution failed with exit code {process.ExitCode}: {errorOutput}";
                return summary;
            }

            // Parse BenchmarkDotNet results from JSON
            var resultsPath = Path.Combine(projectPath, "BenchmarkDotNet.Artifacts", "results");
            if (Directory.Exists(resultsPath))
            {
                var jsonFiles = Directory.GetFiles(resultsPath, "*-report-full.json");
                foreach (var jsonFile in jsonFiles)
                {
                    var results = await ParseBenchmarkResultsAsync(jsonFile);
                    summary.Results.AddRange(results);
                }
            }

            summary.Success = true;
        }
        catch (Exception ex)
        {
            summary.Success = false;
            summary.ErrorMessage = ex.Message;
        }

        return summary;
    }

    private async Task<List<BenchmarkResult>> ParseBenchmarkResultsAsync(string jsonFilePath)
    {
        var results = new List<BenchmarkResult>();

        try
        {
            var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
            var document = JsonDocument.Parse(jsonContent);
            
            if (document.RootElement.TryGetProperty("Benchmarks", out var benchmarks))
            {
                foreach (var benchmark in benchmarks.EnumerateArray())
                {
                    var result = new BenchmarkResult();

                    if (benchmark.TryGetProperty("FullName", out var fullName))
                    {
                        var name = fullName.GetString() ?? string.Empty;
                        var parts = name.Split('.');
                        result.Name = parts.Length > 0 ? parts[^1] : name;
                    }

                    if (benchmark.TryGetProperty("Method", out var method))
                    {
                        result.Method = method.GetString() ?? string.Empty;
                    }

                    if (benchmark.TryGetProperty("Statistics", out var stats))
                    {
                        if (stats.TryGetProperty("Mean", out var mean))
                        {
                            result.MeanNanoseconds = mean.GetDouble();
                        }

                        if (stats.TryGetProperty("StandardDeviation", out var stdDev))
                        {
                            result.StdDevNanoseconds = stdDev.GetDouble();
                        }
                    }

                    if (benchmark.TryGetProperty("Memory", out var memory))
                    {
                        if (memory.TryGetProperty("BytesAllocatedPerOperation", out var allocated))
                        {
                            result.AllocatedBytes = allocated.GetDouble();
                        }
                    }

                    results.Add(result);
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but continue processing other files
            Console.WriteLine($"Error parsing {jsonFilePath}: {ex.Message}");
        }

        return results;
    }
}
