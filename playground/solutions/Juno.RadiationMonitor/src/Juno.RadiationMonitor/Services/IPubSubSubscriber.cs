using Juno.RadiationMonitor.Models;

namespace Juno.RadiationMonitor.Services;

public interface IPubSubSubscriber
{
    IAsyncEnumerable<RadiationReading> SubscribeAsync(string topic, CancellationToken cancellationToken);
}
