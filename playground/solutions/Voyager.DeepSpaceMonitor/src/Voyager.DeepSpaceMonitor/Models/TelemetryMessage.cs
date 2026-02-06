namespace Voyager.DeepSpaceMonitor.Models;

public class TelemetryMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public bool IsDelayed { get; set; }
    public List<SubMessage> SubMessages { get; set; } = new();
}

public class SubMessage
{
    public string SubId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
}
