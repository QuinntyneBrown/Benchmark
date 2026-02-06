using System.Text.Json.Serialization;

namespace Juno.RadiationMonitor.Models;

public readonly struct RadiationReading
{
    public long TimestampTicks { get; init; }
    public double Value { get; init; }
    public byte SourceIndex { get; init; }
    public byte MetricIndex { get; init; }
    public bool IsDelayed { get; init; }
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

[JsonSerializable(typeof(RadiationReading))]
[JsonSerializable(typeof(RadiationReading[]))]
[JsonSerializable(typeof(TelemetryMessage))]
[JsonSerializable(typeof(TelemetryMessage[]))]
[JsonSerializable(typeof(List<TelemetryMessage>))]
[JsonSerializable(typeof(RadiationBatchEvent))]
internal partial class TelemetryJsonContext : JsonSerializerContext
{
}

public class RadiationBatchEvent
{
    public int BatchSize { get; set; }
    public int DelayedCount { get; set; }
    public long TimestampTicks { get; set; }
    public long DroppedTotal { get; set; }
}
