using Orion.AttitudeTracker.Models;

namespace Orion.AttitudeTracker.Services;

public interface IPubSubSubscriber
{
    IAsyncEnumerable<TelemetryMessage> SubscribeAsync(string topic, CancellationToken cancellationToken);
}
