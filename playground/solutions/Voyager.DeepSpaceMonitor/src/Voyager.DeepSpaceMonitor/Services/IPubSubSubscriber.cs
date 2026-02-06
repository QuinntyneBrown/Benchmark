using Voyager.DeepSpaceMonitor.Models;

namespace Voyager.DeepSpaceMonitor.Services;

public interface IPubSubSubscriber
{
    IAsyncEnumerable<TelemetryMessage> SubscribeAsync(string topic, CancellationToken cancellationToken);
}
