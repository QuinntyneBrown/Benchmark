# Interpreting Reports

The `bn run-and-report` command produces two markdown reports, each targeting a different audience.

## Technical Report (BenchmarkReport.md)

This report is designed for developers and contains detailed performance data.

### Sections

#### Executive Summary

Shows the total number of benchmark projects, how many succeeded or failed, and the total number of individual benchmarks executed.

#### Performance Overview

Aggregate statistics across all benchmarks:
- **Average Throughput** — operations per second across all benchmarks
- **Average Memory Allocation** — mean heap memory allocated per operation

#### Detailed Results (per project)

Each benchmark project gets its own section containing:

**Performance Metrics Table**

| Column | Description |
|--------|-------------|
| Benchmark | The method being measured |
| Mean Time | Average execution time per invocation |
| Std Dev | Standard deviation — lower means more consistent |
| Throughput (ops/sec) | How many times per second the operation can execute (calculated as 1,000,000,000 / mean nanoseconds) |
| Memory Allocated | Heap memory allocated per operation |

**Quality Analysis**

Highlights the fastest operation, slowest operation, and most memory-intensive operation in the project.

**Performance Limits & Recommendations**

Warnings are raised when:
- An operation takes longer than **100ms** (flagged as slow)
- An operation allocates more than **100KB** of memory (flagged as high memory)

#### Interpretation Guide (Appendix)

Definitions of the metrics and category thresholds used throughout the report.

### Performance Categories

| Category | Threshold |
|----------|-----------|
| Fast | < 1ms |
| Moderate | 1–100ms |
| Slow | > 100ms |

### Memory Categories

| Category | Threshold |
|----------|-----------|
| Low | < 1KB |
| Moderate | 1–100KB |
| High | > 100KB |

---

## Product Owner Report (ProductOwnerReport.md)

This report is designed for non-technical stakeholders — product owners, project managers, and executives. It uses plain language and focuses on system health and risk.

### Sections

#### System Health at a Glance

A quick pass/fail count showing how many systems were successfully benchmarked and how many failed.

#### Message Processing Capacity

This section appears when the solution includes **SignalR-based real-time messaging** (E2E benchmarks). It shows how fast each system can process messages end-to-end.

| Column | Description |
|--------|-------------|
| System | The project/application name |
| Time per Message | How long one full message round-trip takes |
| Est. Messages per Second | Throughput — how many messages the system can handle per second |
| Memory per Message | How much memory each message consumes |
| Verdict | Health assessment (see Verdicts below) |

**Crash Risk Assessment** follows the table, highlighting systems with:
- **Memory pressure** — high memory per message could lead to crashes under load
- **Processing backlog risk** — slow processing could cause message queues to back up

#### Internal Processing Performance

Shows unit-level operation performance grouped by project.

| Column | Description |
|--------|-------------|
| Operation | The method being measured |
| Speed | Execution time per call |
| Memory Used | Memory allocated per call |
| Status | Health assessment (see Verdicts below) |

#### Where to Focus Development Effort

A ranked list of the **top 10 optimization opportunities**, sorted by impact score. The impact score considers both speed and memory:

```
Impact = MeanNanoseconds × (1 + AllocatedBytes / 1024)
```

Operations that are both slow and memory-heavy rank highest. Each entry includes a reason:
- "slow processing" (> 100ms)
- "moderate processing time" (> 10ms)
- "high memory usage" (> 100KB)
- "moderate memory usage" (> 10KB)
- "optimization opportunity" (general improvement candidate)

#### Systems That Failed Testing

Lists any systems that could not be benchmarked, with error details for investigation.

#### How to Read This Report (Glossary)

Definitions of all metrics and verdicts in plain language.

### Verdicts

Both the Message Processing Capacity and Internal Processing Performance sections use the same verdict system:

| Verdict | Speed Threshold | Memory Threshold | Meaning |
|---------|----------------|-------------------|---------|
| **Healthy** | ≤ 1ms | ≤ 10KB | Operating well within acceptable limits |
| **Acceptable** | ≤ 10ms | ≤ 100KB | Within normal range, no immediate action needed |
| **Needs Investigation** | ≤ 100ms | ≤ 500KB | Performance is degraded — worth investigating |
| **At Risk** | > 100ms | > 500KB | Significant performance concern — prioritize for improvement |

A system receives the verdict matching the **worse** of its speed and memory thresholds. For example, a system with 5ms response time (Acceptable) but 200KB memory (Needs Investigation) receives **Needs Investigation**.

---

## Comparing Solutions

When benchmarking multiple solutions (e.g., in a playground with several projects), use these strategies:

1. **Throughput comparison** — compare ops/sec in the technical report to see which implementations are fastest
2. **Memory efficiency** — compare memory allocation to identify which implementations are most efficient
3. **Verdict comparison** — in the product owner report, compare verdicts across systems for a quick health overview
4. **Focus ranking** — the "Where to Focus Development Effort" section ranks all operations across all projects, making cross-project comparison straightforward

## Tips

- **Standard deviation matters** — a fast operation with high std dev may be unreliable under load
- **Memory is cumulative** — small allocations per call add up under high throughput
- **Verdicts are conservative** — "Acceptable" is genuinely fine for most use cases; only "At Risk" warrants urgent attention
- **Run benchmarks in Release mode** — the tool does this automatically, but if running manually, always use `-c Release`
