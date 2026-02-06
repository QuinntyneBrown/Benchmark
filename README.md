# Benchmark

A powerful CLI tool for automatically generating BenchmarkDotNet projects and performance reports for .NET solutions.

## Features

- **Unit Benchmark Generation**: Automatically creates benchmarks for every public class and method in your solution
- **E2E Benchmark Generation**: Creates end-to-end benchmarks for Web APIs (using WebApplicationFactory) and Console applications
- **SignalR E2E Detection**: Automatically discovers SignalR hubs and generates full message round-trip benchmarks
- **Benchmark Results Reporting**: Run benchmarks and generate comprehensive markdown reports with performance metrics, throughput analysis, and quality insights
- **Dual Reports**: Technical report for developers and product owner report for stakeholders
- **File-Per-Command Architecture**: Uses System.CommandLine with proper separation of concerns
- **Microsoft Extensions Integration**: Built with DI, Configuration, Logging, and Options pattern
- **BenchmarkDotNet Integration**: Generates projects ready to run with BenchmarkDotNet

## Installation

Install as a global .NET tool:

```bash
dotnet tool install -g QuinntyneBrown.Benchmark.Cli
```

Update to the latest version:

```bash
dotnet tool update -g QuinntyneBrown.Benchmark.Cli
```

## Usage

### Generate Benchmarks

```bash
bn generate <path-to-solution>
```

This generates both unit and E2E benchmark projects for your solution. Accepts both `.sln` and `.slnx` solution files.

### Run Benchmarks and Generate Reports

```bash
bn run-and-report <path-to-solution>
```

Specify a custom output path for the report:

```bash
bn run-and-report <path-to-solution> --output ./reports/benchmark-results.md
```

This command will:
1. Generate unit and E2E benchmark projects
2. Run all benchmarks (this may take several minutes)
3. Collect and analyze results
4. Generate two comprehensive markdown reports:
   - **BenchmarkReport.md** — detailed metrics for developers
   - **ProductOwnerReport.md** — health summary for stakeholders

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

## Documentation

- [Getting Started](docs/getting-started.md) — installation, prerequisites, and quick start
- [Command Reference](docs/commands.md) — full CLI reference for all commands
- [Interpreting Reports](docs/interpreting-reports.md) — how to read metrics, categories, and verdicts
- [Architecture](docs/architecture.md) — how the tool works internally

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
├── docs/                       # User guides and reference documentation
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
- A valid .NET solution file (.sln or .slnx)

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
