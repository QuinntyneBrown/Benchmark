# Getting Started

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- A .NET solution file (`.sln` or `.slnx`)

## Installation

Install the CLI tool globally via NuGet:

```bash
dotnet tool install -g QuinntyneBrown.Benchmark.Cli
```

To update to the latest version:

```bash
dotnet tool update -g QuinntyneBrown.Benchmark.Cli
```

To uninstall:

```bash
dotnet tool uninstall -g QuinntyneBrown.Benchmark.Cli
```

## Quick Start

### 1. Generate Benchmark Projects

Point the tool at your solution file to auto-generate benchmark projects:

```bash
bn generate path/to/YourSolution.sln
```

This creates two projects in your solution directory:
- `YourSolution.UnitBenchmarks` — benchmarks for every public class and method
- `YourSolution.E2EBenchmarks` — end-to-end benchmarks for Web APIs and Console apps

### 2. Generate Benchmarks, Run Them, and Get Reports

For the full workflow in a single command:

```bash
bn run-and-report path/to/YourSolution.sln
```

This will:
1. Generate benchmark projects
2. Run all benchmarks (may take several minutes)
3. Produce a technical `BenchmarkReport.md`
4. Produce a non-technical `ProductOwnerReport.md`

### 3. Review the Reports

- **BenchmarkReport.md** — detailed performance metrics for developers
- **ProductOwnerReport.md** — high-level health summary for stakeholders

See [Interpreting Reports](interpreting-reports.md) for a full guide on reading these reports.

## Next Steps

- [CLI Command Reference](commands.md) — all commands, arguments, and options
- [Interpreting Reports](interpreting-reports.md) — understand metrics, categories, and verdicts
- [Architecture](architecture.md) — how the tool works internally
