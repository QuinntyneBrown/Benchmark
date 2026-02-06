namespace Artemis.TelemetryRelay.Models;

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
