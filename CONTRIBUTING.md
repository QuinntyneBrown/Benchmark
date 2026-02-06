# Contributing to Benchmark

Thanks for your interest in contributing! This guide covers how to set up the project, make changes, and submit a pull request.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Git

## Getting Started

1. Fork and clone the repository:

```bash
git clone https://github.com/QuinntyneBrown/Benchmark.git
cd Benchmark
```

2. Build the solution:

```bash
dotnet build
```

3. Verify the CLI runs:

```bash
dotnet run --project src/Benchmark.Cli -- --help
```

## Project Layout

```
src/
  Benchmark.Cli/           CLI entry point (System.CommandLine)
    Commands/               One file per command (generate, run-and-report)
  Benchmark.Core/           Core library
    Models/                 Domain models (ProjectInfo, ClassInfo, BenchmarkResult, etc.)
    Services/               Business logic (SolutionAnalyzer, BenchmarkProjectGenerator,
                            BenchmarkRunner, ReportGenerator)
docs/                       User-facing documentation
playground/solutions/       Sample .NET solutions for testing
```

## Development Workflow

1. Create a branch from `main`:

```bash
git checkout -b your-feature-name
```

2. Make your changes.

3. Build and verify:

```bash
dotnet build
```

4. Test against one of the playground solutions:

```bash
dotnet run --project src/Benchmark.Cli -- generate playground/solutions/Apollo.SignalProcessor/Apollo.SignalProcessor.slnx
```

5. Commit and push your branch, then open a pull request against `main`.

## Areas of Contribution

- **New benchmark strategies** — additional E2E patterns beyond SignalR, HTTP, and Console
- **Report improvements** — new metrics, visualizations, or report formats
- **Roslyn analysis** — broader detection of project types, middleware, or frameworks
- **Constructor provisioning** — support for additional DI parameter types in generated benchmarks
- **Documentation** — improvements to docs/ or inline code comments

## Code Conventions

- Target **.NET 9.0**
- Use **file-scoped namespaces** (`namespace Foo;`)
- Follow existing naming: `PascalCase` for public members, `_camelCase` for private fields
- Keep classes in the layer they belong to — CLI concerns in `Benchmark.Cli`, logic in `Benchmark.Core`
- Avoid adding dependencies unless necessary

## Reporting Issues

Open an issue at [github.com/QuinntyneBrown/Benchmark/issues](https://github.com/QuinntyneBrown/Benchmark/issues) with:

- Steps to reproduce
- Expected vs actual behavior
- .NET SDK version (`dotnet --version`)
- Solution format used (`.sln` or `.slnx`)

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
