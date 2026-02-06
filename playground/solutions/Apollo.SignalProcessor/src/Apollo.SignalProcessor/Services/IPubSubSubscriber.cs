using Apollo.SignalProcessor.Models;

namespace Apollo.SignalProcessor.Services;

public interface IPubSubSubscriber
{
    IAsyncEnumerable<TelemetryMessage> SubscribeAsync(string topic, CancellationToken cancellationToken);
}
