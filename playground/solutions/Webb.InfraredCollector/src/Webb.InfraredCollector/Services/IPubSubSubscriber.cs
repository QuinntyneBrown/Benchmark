using Webb.InfraredCollector.Models;

namespace Webb.InfraredCollector.Services;

public interface IPubSubSubscriber
{
    IAsyncEnumerable<TelemetryMessage> SubscribeAsync(string topic, CancellationToken cancellationToken);
}
