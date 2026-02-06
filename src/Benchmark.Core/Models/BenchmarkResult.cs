namespace Benchmark.Core.Models;

public class BenchmarkResult
{
    public string Name { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public double MeanNanoseconds { get; set; }
    public double StdDevNanoseconds { get; set; }
    public double AllocatedBytes { get; set; }
    public string Error { get; set; } = string.Empty;
    
    public double MeanMicroseconds => MeanNanoseconds / 1000.0;
    public double MeanMilliseconds => MeanNanoseconds / 1_000_000.0;
    public double MeanSeconds => MeanNanoseconds / 1_000_000_000.0;
    
    public double AllocatedKilobytes => AllocatedBytes / 1024.0;
    public double AllocatedMegabytes => AllocatedBytes / (1024.0 * 1024.0);
}

public class BenchmarkSummary
{
    public string ProjectName { get; set; } = string.Empty;
    public List<BenchmarkResult> Results { get; set; } = new();
    public DateTime ExecutionTime { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
