namespace Voyager.DeepSpaceMonitor.Configuration;

public class TelemetryOptions
{
    public const string SectionName = "Telemetry";
    public int MessageRateHz { get; set; } = 10;
    public int DelayThresholdMs { get; set; } = 2000;
    public int MaxParallelTasks { get; set; } = 8;
}
