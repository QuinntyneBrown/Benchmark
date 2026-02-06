using Artemis.TelemetryRelay.Models;

namespace Artemis.TelemetryRelay.Services;

public interface IPubSubSubscriber
{
    IAsyncEnumerable<TelemetryMessage> SubscribeAsync(string topic, CancellationToken cancellationToken);
}
