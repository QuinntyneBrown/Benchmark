using Hubble.ImageTelemetry.Models;

namespace Hubble.ImageTelemetry.Services;

public interface IPubSubSubscriber
{
    IAsyncEnumerable<TelemetryMessage> SubscribeAsync(string topic, CancellationToken cancellationToken);
}
