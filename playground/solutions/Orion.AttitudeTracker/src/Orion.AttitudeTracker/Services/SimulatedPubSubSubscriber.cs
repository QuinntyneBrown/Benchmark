using Orion.AttitudeTracker.Models;
using Orion.AttitudeTracker.Configuration;
using Microsoft.Extensions.Options;

namespace Orion.AttitudeTracker.Services;

public class SimulatedPubSubSubscriber : IPubSubSubscriber
{
    private readonly TelemetryOptions _options;
    private readonly ILogger<SimulatedPubSubSubscriber> _logger;
    private readonly Random _random = new();

    private static readonly string[] Sources =
    {
        "ORION-IMU-1", "ORION-IMU-2", "STAR-TRACKER-A", "STAR-TRACKER-B",
        "SUN-SENSOR-1", "SUN-SENSOR-2", "EARTH-SENSOR", "MOON-SENSOR",
        "RCS-THRUSTER-BANK"
    };

    private static readonly string[] MetricTypes =
    {
        "ROLL_DEG", "PITCH_DEG", "YAW_DEG", "ROLL_RATE_DPS", "PITCH_RATE_DPS",
        "YAW_RATE_DPS", "QUATERNION_W", "QUATERNION_X", "QUATERNION_Y", "QUATERNION_Z"
    };

    private static readonly string[] Units =
    { "deg", "deg", "deg", "deg/s", "deg/s", "deg/s", "", "", "", "" };

    public SimulatedPubSubSubscriber(IOptions<TelemetryOptions> options, ILogger<SimulatedPubSubSubscriber> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<TelemetryMessage> SubscribeAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Orion attitude tracker subscribing to {Topic}", topic);
        int sequenceNumber = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var messageCount = _random.Next(50, 200);

            for (int i = 0; i < messageCount; i++)
            {
                var metricIndex = _random.Next(MetricTypes.Length);
                var sourceIndex = _random.Next(Sources.Length);

                // Use predictable IDs to allow dedup (source + sequence combo)
                yield return new TelemetryMessage
                {
                    MessageId = $"{Sources[sourceIndex]}-{sequenceNumber++ % 1000}",
                    Source = Sources[sourceIndex],
                    Timestamp = DateTime.UtcNow.AddMilliseconds(-_random.Next(0, 5000)),
                    Value = Math.Round(_random.NextDouble() * 360 - 180, 6),
                    Unit = Units[metricIndex],
                    MetricType = MetricTypes[metricIndex]
                };
            }

            await Task.Delay(1000 / _options.MessageRateHz, cancellationToken);
        }
    }
}
