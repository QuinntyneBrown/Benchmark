namespace Hubble.ImageTelemetry.Configuration;

public class TelemetryOptions
{
    public const string SectionName = "Telemetry";
    public int MessageRateHz { get; set; } = 10;
    public int DelayThresholdMs { get; set; } = 2000;
    public int FrameSize { get; set; } = 20;
    public int SlidingExpirationSeconds { get; set; } = 60;
    public int StreamCount { get; set; } = 4;
}
