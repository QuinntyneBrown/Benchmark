using Artemis.TelemetryRelay.Models;
using Microsoft.Extensions.Options;
using Artemis.TelemetryRelay.Configuration;

namespace Artemis.TelemetryRelay.Services;

public class SimulatedPubSubSubscriber : IPubSubSubscriber
{
    private readonly TelemetryOptions _options;
    private readonly ILogger<SimulatedPubSubSubscriber> _logger;

    private static readonly string[] Sources =
    {
        "LUNAR-RELAY-ALPHA", "LUNAR-RELAY-BETA", "LUNAR-RELAY-GAMMA",
        "ARTEMIS-GATEWAY", "ORION-CAPSULE", "LUNAR-SURFACE-1",
        "LUNAR-SURFACE-2", "COMM-SAT-L1", "COMM-SAT-L2"
    };

    private static readonly string[] MetricTypes =
    {
        "SIGNAL_STRENGTH", "LATENCY_MS", "PACKET_LOSS", "BANDWIDTH_MBPS",
        "TEMPERATURE_C", "POWER_WATTS", "ATTITUDE_DEG", "VELOCITY_MPS",
        "ALTITUDE_KM", "FUEL_PERCENT"
    };

    private static readonly string[] Units =
    { "dBm", "ms", "%", "Mbps", "°C", "W", "°", "m/s", "km", "%" };

    public SimulatedPubSubSubscriber(IOptions<TelemetryOptions> options, ILogger<SimulatedPubSubSubscriber> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<TelemetryMessage> SubscribeAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscribing to topic {Topic} at {Rate}Hz", topic, _options.MessageRateHz);

        while (!cancellationToken.IsCancellationRequested)
        {
            // Deliberately naive: new Random each iteration (bad practice)
            var random = new Random();
            var messageCount = random.Next(50, 200);

            for (int i = 0; i < messageCount; i++)
            {
                var metricIndex = random.Next(MetricTypes.Length);

                // Deliberately allocating new strings via concatenation
                var message = new TelemetryMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Source = Sources[random.Next(Sources.Length)] + "-" + DateTime.UtcNow.Ticks.ToString(),
                    Timestamp = DateTime.UtcNow.AddMilliseconds(-random.Next(0, 5000)),
                    Value = Math.Round(random.NextDouble() * 1000, 4),
                    Unit = Units[metricIndex],
                    MetricType = MetricTypes[metricIndex],
                    IsDelayed = false
                };

                yield return message;
            }

            await Task.Delay(1000 / _options.MessageRateHz, cancellationToken);
        }
    }
}
