namespace Juno.RadiationMonitor.Configuration;

public class TelemetryOptions
{
    public const string SectionName = "Telemetry";
    public int MessageRateHz { get; set; } = 10;
    public int DelayThresholdMs { get; set; } = 2000;
    public int ChannelCapacity { get; set; } = 512;
    public int DispatchBatchSize { get; set; } = 64;
    public int PoolArraySize { get; set; } = 128;
}
