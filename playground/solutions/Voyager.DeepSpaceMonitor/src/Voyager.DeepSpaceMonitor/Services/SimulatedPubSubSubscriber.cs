using Voyager.DeepSpaceMonitor.Models;
using Voyager.DeepSpaceMonitor.Configuration;
using Microsoft.Extensions.Options;

namespace Voyager.DeepSpaceMonitor.Services;

public class SimulatedPubSubSubscriber : IPubSubSubscriber
{
    private readonly TelemetryOptions _options;
    private readonly ILogger<SimulatedPubSubSubscriber> _logger;
    private readonly Random _random = new();

    private static readonly string[] Sources =
    {
        "VOYAGER-1-PWS", "VOYAGER-1-MAG", "VOYAGER-1-LECP",
        "VOYAGER-2-PWS", "VOYAGER-2-MAG", "VOYAGER-2-LECP",
        "DSN-GOLDSTONE-70M", "DSN-MADRID-70M", "DSN-CANBERRA-70M"
    };

    private static readonly string[] MetricTypes =
    {
        "PLASMA_WAVE", "MAGNETIC_FIELD", "COSMIC_RAY", "RADIO_EMISSION",
        "HELIOSPHERE_BOUNDARY", "PARTICLE_FLUX", "SIGNAL_POWER",
        "BIT_RATE", "DISTANCE_AU", "LIGHT_TIME_HOURS"
    };

    private static readonly string[] Units =
    { "V/m", "nT", "particles/cm²/s", "W/m²", "AU", "p/cm²/s/sr", "dBm", "bps", "AU", "hours" };

    private static readonly string[] SubCategories =
    { "THERMAL", "ELECTRICAL", "MECHANICAL", "RADIATION", "OPTICAL" };

    public SimulatedPubSubSubscriber(IOptions<TelemetryOptions> options, ILogger<SimulatedPubSubSubscriber> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<TelemetryMessage> SubscribeAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Voyager deep space monitor subscribing to {Topic}", topic);

        while (!cancellationToken.IsCancellationRequested)
        {
            var messageCount = _random.Next(50, 200);

            for (int i = 0; i < messageCount; i++)
            {
                var metricIndex = _random.Next(MetricTypes.Length);
                var subMessageCount = _random.Next(2, 6);

                var message = new TelemetryMessage
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    Source = Sources[_random.Next(Sources.Length)],
                    Timestamp = DateTime.UtcNow.AddMilliseconds(-_random.Next(0, 5000)),
                    Value = Math.Round(_random.NextDouble() * 1000, 4),
                    Unit = Units[metricIndex],
                    MetricType = MetricTypes[metricIndex]
                };

                for (int j = 0; j < subMessageCount; j++)
                {
                    message.SubMessages.Add(new SubMessage
                    {
                        SubId = $"{message.MessageId}-sub-{j}",
                        Category = SubCategories[_random.Next(SubCategories.Length)],
                        Value = Math.Round(_random.NextDouble() * 100, 4),
                        Unit = Units[_random.Next(Units.Length)]
                    });
                }

                yield return message;
            }

            await Task.Delay(1000 / _options.MessageRateHz, cancellationToken);
        }
    }
}
