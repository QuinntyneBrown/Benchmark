# Benchmark

A powerful CLI tool for automatically generating BenchmarkDotNet projects for .NET solutions.

## Features

- **Unit Benchmark Generation**: Automatically creates benchmarks for every public class and method in your solution
- **E2E Benchmark Generation**: Creates end-to-end benchmarks for Web APIs (using WebApplicationFactory) and Console applications
- **Benchmark Results Reporting**: Run benchmarks and automatically generate comprehensive markdown reports with performance metrics, throughput analysis, and quality insights
- **File-Per-Command Architecture**: Uses System.CommandLine with proper separation of concerns
- **Microsoft Extensions Integration**: Built with DI, Configuration, Logging, and Options pattern
- **BenchmarkDotNet Integration**: Generates projects ready to run with BenchmarkDotNet

## Installation

Build the CLI tool:

```bash
dotnet build
```

## Usage

### Generate Benchmarks

To generate both unit and E2E benchmark projects for your solution:

```bash
dotnet run --project src/Benchmark.Cli -- generate <path-to-solution.sln>
```

### Run Benchmarks and Generate Reports

To generate benchmarks, run them, and create a comprehensive markdown report in one command:

```bash
dotnet run --project src/Benchmark.Cli -- run-and-report <path-to-solution.sln>
```

Specify a custom output path for the report:

```bash
dotnet run --project src/Benchmark.Cli -- run-and-report <path-to-solution.sln> --output ./reports/benchmark-results.md
```

This command will:
1. Generate unit and E2E benchmark projects
2. Run all benchmarks (this may take several minutes)
3. Collect and analyze results
4. Generate a comprehensive markdown report with:
   - Executive summary with total benchmarks and success rates
   - Performance overview with average throughput and memory usage
   - Detailed metrics for each benchmark project
   - Quality analysis identifying fastest/slowest operations
   - Performance warnings for high memory or slow operations
   - Interpretation guide for understanding the metrics

### What Gets Generated

The tool creates two benchmark projects in your solution directory:

1. **{SolutionName}.UnitBenchmarks**: Contains benchmarks for all public methods in your classes
2. **{SolutionName}.E2EBenchmarks**: Contains end-to-end benchmarks for Web APIs and Console applications

### Running the Generated Benchmarks Manually

If you prefer to run benchmarks manually without generating reports:

```bash
cd {SolutionName}.UnitBenchmarks
dotnet run -c Release

cd {SolutionName}.E2EBenchmarks
dotnet run -c Release
```

### Example Report Output

The generated markdown report includes:

- **Executive Summary**: Overview of benchmark runs and total operations tested
- **Performance Overview**: Average throughput and memory allocation statistics
- **Detailed Metrics Tables**: Mean execution time, standard deviation, throughput, and memory for each benchmark
- **Quality Analysis**: Identification of fastest, slowest, and most memory-intensive operations
- **Performance Warnings**: Automatic detection of operations exceeding performance thresholds
- **Interpretation Guide**: Help understanding the metrics and performance categories

## Project Structure

```
├── src/
│   ├── Benchmark.Cli/          # CLI application with System.CommandLine
│   │   ├── Commands/           # Command implementations
│   │   ├── Options/            # Configuration options
│   │   └── Program.cs          # Entry point with DI setup
│   └── Benchmark.Core/         # Core library with business logic
│       ├── Models/             # Domain models
│       ├── Services/           # Service implementations
│       └── Generators/         # Code generation utilities
└── Benchmark.sln
```

## Architecture

- **System.CommandLine**: Modern command-line parsing
- **Microsoft.Extensions.DependencyInjection**: Dependency injection
- **Microsoft.Extensions.Logging**: Structured logging
- **Microsoft.Extensions.Configuration**: Configuration management
- **Microsoft.Extensions.Options**: Options pattern
- **Roslyn**: Solution and code analysis
- **BenchmarkDotNet**: Performance benchmarking

## Requirements

- .NET 9.0 or later
- A valid .NET solution file (.sln)

## Examples

### Generated Unit Benchmark Example

```csharp
[MemoryDiagnoser]
public class MyServiceBenchmarks
{
    private MyService? _testSubject;

    [GlobalSetup]
    public void Initialize()
    {
        _testSubject = new MyService();
    }

    [Benchmark]
    public void Measure_ProcessData()
    {
        _testSubject!.ProcessData("sample");
    }
}
```

### Generated E2E Benchmark Example (Web API)

```csharp
[MemoryDiagnoser]
public class MyApiE2EBenchmarks
{
    private HttpClient? _httpClient;

    [GlobalSetup]
    public void Initialize()
    {
        // Configure with WebApplicationFactory
    }

    [Benchmark]
    public async Task MeasureApiEndpoint()
    {
        var result = await _httpClient!.GetAsync("/api/endpoint");
        result.EnsureSuccessStatusCode();
    }
}
```

## License

MIT
