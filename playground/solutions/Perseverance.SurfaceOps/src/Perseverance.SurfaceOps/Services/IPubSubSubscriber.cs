using Perseverance.SurfaceOps.Models;

namespace Perseverance.SurfaceOps.Services;

public interface IPubSubSubscriber
{
    IAsyncEnumerable<TelemetryMessage> SubscribeAsync(string topic, CancellationToken cancellationToken);
}
