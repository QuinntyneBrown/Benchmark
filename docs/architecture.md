# Architecture

This document describes how the benchmark tool works internally.

## Pipeline Overview

```
Solution File (.sln/.slnx)
        │
        ▼
┌─────────────────┐
│ Roslyn Analysis  │  SolutionAnalyzer
│ (Semantic Model) │
└────────┬────────┘
         │ List<ProjectInfo>
         ▼
┌─────────────────────┐
│ Benchmark Generation │  BenchmarkProjectGenerator
│ (Code Templates)     │
└────────┬────────────┘
         │ .cs + .csproj files
         ▼
┌──────────────────────┐
│ BenchmarkDotNet       │  BenchmarkRunner
│ Execution (Release)   │
└────────┬─────────────┘
         │ JSON results
         ▼
┌──────────────────┐
│ Report Generation │  ReportGenerator
│ (Markdown)        │
└──────────────────┘
         │
         ▼
  BenchmarkReport.md
  ProductOwnerReport.md
```

## Stage 1: Roslyn Analysis

**Class:** `SolutionAnalyzer` (implements `ISolutionAnalyzer`)

The analyzer opens the solution using Roslyn's `MSBuildWorkspace`. For `.slnx` files (XML-based solution format), it parses the XML directly with `XDocument` and opens each project individually, since `MSBuildWorkspace` does not support `.slnx`.

For each project, the analyzer:

1. **Detects project type** by inspecting the project file:
   - `Microsoft.NET.Sdk.Web` or `Microsoft.AspNetCore` references → **WebApi**
   - `<OutputType>Exe</OutputType>` → **Console**
   - Otherwise → **ClassLibrary**
   - Web SDK is checked before executable type to avoid misclassification

2. **Extracts public classes and methods** via Roslyn's semantic model:
   - Finds all public, non-abstract classes
   - Collects public methods with their parameters and return types
   - Records first constructor parameters for dependency provisioning
   - Flags infrastructure classes (Hub, BackgroundService, ControllerBase) to exclude from unit benchmarks

3. **Detects SignalR hubs** for E2E benchmarking:
   - Finds `app.MapHub<T>("/path")` invocations to extract the hub type and endpoint
   - Finds `.SendAsync("methodName", ...)` calls to extract the client callback method
   - Detects `.Group("topic")` vs `.All` broadcast patterns to determine subscription behavior

**Output:** `List<ProjectInfo>` containing project metadata, class info, method signatures, and hub endpoint info.

## Stage 2: Benchmark Code Generation

**Class:** `BenchmarkProjectGenerator` (implements `IBenchmarkProjectGenerator`)

Generates two benchmark projects:

### Unit Benchmarks

For each non-infrastructure public class, a benchmark file is generated containing:
- A `[MemoryDiagnoser]` attribute for memory tracking
- A `[GlobalSetup]` method that instantiates the class under test
- A `[Benchmark]` method for each public method

Constructor dependencies are provisioned automatically:
- `ILogger<T>` → `NullLogger<T>.Instance`
- `IOptions<T>` → `Options.Create(new T())`
- `IMemoryCache` → `new MemoryCache(new MemoryCacheOptions())`
- Primitives → default values

### E2E Benchmarks

For each executable project, a benchmark is generated based on project type:

- **WebApi with SignalR** → Full message flow benchmark: creates a `HubConnection`, subscribes to the callback method (and group topic if applicable), then measures round-trip message delivery
- **WebApi without SignalR** → HTTP GET benchmark against the root endpoint using `WebApplicationFactory`
- **Console** → Process launch and exit benchmark

## Stage 3: Benchmark Execution

**Class:** `BenchmarkRunner` (implements `IBenchmarkRunner`)

Runs each benchmark project via:
```
dotnet run -c Release -- --filter * --exporters json
```

- `--filter *` ensures non-interactive execution (without it, `BenchmarkSwitcher` enters interactive mode)
- `--exporters json` produces machine-readable result files
- stdout and stderr are read concurrently before `WaitForExitAsync()` to prevent buffer deadlock

Results are parsed from BenchmarkDotNet's JSON export files (`*-report-full-compressed.json`), extracting mean time, standard deviation, and memory allocation per benchmark.

## Stage 4: Report Generation

**Class:** `ReportGenerator` (implements `IReportGenerator`)

Produces two markdown reports from the collected `BenchmarkSummary` data:

### Technical Report (BenchmarkReport.md)

Contains raw metrics tables, quality analysis, performance warnings, and an interpretation guide. Designed for developers.

### Product Owner Report (ProductOwnerReport.md)

Translates metrics into business language using a verdict system (Healthy / Acceptable / Needs Investigation / At Risk). Includes message processing capacity, crash risk assessment, and a ranked list of optimization opportunities. Designed for non-technical stakeholders.

## Key Design Decisions

- **Roslyn over reflection** — analyzing source code statically allows benchmark generation without compiling or running the target solution first
- **Two report types** — separating technical and business concerns means developers get the data they need while stakeholders get actionable insights
- **SignalR E2E detection** — automated hub discovery eliminates manual configuration for real-time messaging benchmarks
- **BenchmarkDotNet integration** — leveraging the established benchmarking framework ensures statistically rigorous results with proper warmup, iteration, and outlier handling
