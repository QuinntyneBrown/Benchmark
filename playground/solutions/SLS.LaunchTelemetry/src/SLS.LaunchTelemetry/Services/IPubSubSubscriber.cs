using SLS.LaunchTelemetry.Models;

namespace SLS.LaunchTelemetry.Services;

public interface IPubSubSubscriber
{
    IAsyncEnumerable<LaunchReading> SubscribeAsync(string topic, CancellationToken cancellationToken);
}
