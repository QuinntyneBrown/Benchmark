namespace Hubble.ImageTelemetry.Models;

public class TelemetryMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public bool IsDelayed { get; set; }
    public string StreamId { get; set; } = string.Empty;
    public int FrameSequence { get; set; }
}

public class TelemetryFrame
{
    public string FrameId { get; set; } = string.Empty;
    public string StreamId { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public List<TelemetryMessage> Messages { get; set; } = new();
    public DateTime ComposedAt { get; set; }
    public bool HasDelayedMessages { get; set; }
}
