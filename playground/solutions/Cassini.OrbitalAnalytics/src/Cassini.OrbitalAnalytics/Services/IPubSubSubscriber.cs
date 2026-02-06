using Cassini.OrbitalAnalytics.Models;

namespace Cassini.OrbitalAnalytics.Services;

public interface IPubSubSubscriber
{
    IAsyncEnumerable<TelemetryMessage> SubscribeAsync(string topic, CancellationToken cancellationToken);
}
