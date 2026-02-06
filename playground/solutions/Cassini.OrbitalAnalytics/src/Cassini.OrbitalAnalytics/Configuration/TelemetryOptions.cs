namespace Cassini.OrbitalAnalytics.Configuration;

public class TelemetryOptions
{
    public const string SectionName = "Telemetry";
    public int MessageRateHz { get; set; } = 10;
    public int DelayThresholdMs { get; set; } = 2000;
    public int CacheSizeLimit { get; set; } = 10000;
    public int CacheAbsoluteExpirationSeconds { get; set; } = 300;
    public int CacheSlidingExpirationSeconds { get; set; } = 60;
    public int RateLimitPermitsPerSecond { get; set; } = 1000;
    public int RateLimitQueueLimit { get; set; } = 500;
    public int BatchSize { get; set; } = 25;
}
