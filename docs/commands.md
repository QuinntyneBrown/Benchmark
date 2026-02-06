# CLI Command Reference

The CLI is invoked via the `bn` command after installation.

## `bn generate`

Generate benchmark projects for a .NET solution.

### Usage

```bash
bn generate <solution-path>
```

### Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `solution-path` | Yes | Path to the .NET solution file (`.sln` or `.slnx`) |

### What It Does

1. Analyzes the solution using Roslyn to discover all projects, public classes, and methods
2. Detects project types (Web API, Console, Class Library) and SignalR hubs
3. Generates a **Unit Benchmark** project with benchmarks for every public method
4. Generates an **E2E Benchmark** project with end-to-end benchmarks for executable projects
5. Adds both projects to the solution

### Output

Two new projects are created in the solution directory:

- `{SolutionName}.UnitBenchmarks/`
- `{SolutionName}.E2EBenchmarks/`

### Examples

```bash
# Generate benchmarks for a solution
bn generate ./MyApp.sln

# Using an absolute path
bn generate C:/projects/MyApp/MyApp.sln

# Using the .slnx format
bn generate ./MyApp.slnx
```

---

## `bn run-and-report`

Generate benchmarks, run them, and create comprehensive markdown reports in one command.

### Usage

```bash
bn run-and-report <solution-path> [--output <path>]
```

### Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `solution-path` | Yes | Path to the .NET solution file (`.sln` or `.slnx`) |

### Options

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--output` | No | `BenchmarkReport.md` in solution directory | Output path for the technical markdown report |

### What It Does

1. **Step 1/4 — Generate**: Creates unit and E2E benchmark projects (same as `bn generate`)
2. **Step 2/4 — Run**: Executes all benchmarks in Release configuration (may take several minutes)
3. **Step 3/4 — Report**: Generates two markdown reports:
   - **BenchmarkReport.md** — technical report with detailed metrics
   - **ProductOwnerReport.md** — stakeholder-friendly summary (always written to the same directory as the technical report)
4. **Step 4/4 — Summary**: Prints report file paths

### Examples

```bash
# Run full workflow with default report location
bn run-and-report ./MyApp.sln

# Specify a custom output path for the technical report
bn run-and-report ./MyApp.sln --output ./reports/benchmark-results.md

# The product owner report is always placed alongside the technical report
# In this case: ./reports/ProductOwnerReport.md
```

### Notes

- Benchmarks run in Release configuration for accurate results
- The process may take several minutes depending on the number of benchmarks
- Both unit and E2E benchmarks are executed and their results combined in the reports
- If a benchmark project fails to run, it is recorded as a failed run in the report
