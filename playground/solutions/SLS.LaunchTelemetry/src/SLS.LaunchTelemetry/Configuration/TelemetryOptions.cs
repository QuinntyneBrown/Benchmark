namespace SLS.LaunchTelemetry.Configuration;

public class TelemetryOptions
{
    public const string SectionName = "Telemetry";
    public int MessageRateHz { get; set; } = 10;
    public int DelayThresholdMs { get; set; } = 2000;
    public int PipeBufferSize { get; set; } = 65536;
    public int DispatchBatchSize { get; set; } = 128;
    public int ArrayPoolMaxSize { get; set; } = 4096;
}
