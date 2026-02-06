using System.Runtime.InteropServices;

namespace SLS.LaunchTelemetry.Models;

[Flags]
public enum TelemetryFlags : byte
{
    None = 0,
    IsDelayed = 1,
    IsCritical = 2,
    IsCompressed = 4
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LaunchReading
{
    public long TimestampTicks;
    public double Value;
    public byte SourceIndex;
    public byte MetricIndex;
    public TelemetryFlags Flags;

    public readonly bool IsDelayed => (Flags & TelemetryFlags.IsDelayed) != 0;
}

public class TelemetryMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public bool IsDelayed { get; set; }
}
