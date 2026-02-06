namespace Perseverance.SurfaceOps.Configuration;

public class TelemetryOptions
{
    public const string SectionName = "Telemetry";
    public int MessageRateHz { get; set; } = 10;
    public int DelayThresholdMs { get; set; } = 2000;
    public int BatchWindowMs { get; set; } = 200;
    public int MaxBatchSize { get; set; } = 50;
    public int MaxConcurrency { get; set; } = 4;
}
