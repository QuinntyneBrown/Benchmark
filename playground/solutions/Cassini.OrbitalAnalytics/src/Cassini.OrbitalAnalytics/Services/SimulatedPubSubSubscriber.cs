using Cassini.OrbitalAnalytics.Models;
using Cassini.OrbitalAnalytics.Configuration;
using Microsoft.Extensions.Options;

namespace Cassini.OrbitalAnalytics.Services;

public class SimulatedPubSubSubscriber : IPubSubSubscriber
{
    private readonly TelemetryOptions _options;
    private readonly ILogger<SimulatedPubSubSubscriber> _logger;
    private readonly Random _random = new();

    private static readonly string[] Sources =
    {
        "CASSINI-ISS-NAC", "CASSINI-ISS-WAC", "CASSINI-VIMS",
        "CASSINI-CIRS", "CASSINI-UVIS", "CASSINI-RADAR",
        "CASSINI-MAG", "CASSINI-CDA", "HUYGENS-DISR"
    };

    private static readonly string[] MetricTypes =
    {
        "ORBITAL_VELOCITY_KMS", "ALTITUDE_KM", "INCLINATION_DEG", "ECCENTRICITY",
        "RING_PARTICLE_DENSITY", "MAGNETIC_FIELD_NT", "TITAN_DISTANCE_KM",
        "DUST_FLUX", "PLASMA_TEMP_EV", "GRAVITY_ANOMALY"
    };

    private static readonly string[] Units =
    { "km/s", "km", "deg", "ratio", "p/m³", "nT", "km", "p/m²/s", "eV", "mGal" };

    public SimulatedPubSubSubscriber(IOptions<TelemetryOptions> options, ILogger<SimulatedPubSubSubscriber> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<TelemetryMessage> SubscribeAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cassini orbital analytics subscribing to {Topic}", topic);

        while (!cancellationToken.IsCancellationRequested)
        {
            var messageCount = _random.Next(50, 200);

            for (int i = 0; i < messageCount; i++)
            {
                var metricIndex = _random.Next(MetricTypes.Length);

                yield return new TelemetryMessage
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    Source = Sources[_random.Next(Sources.Length)],
                    Timestamp = DateTime.UtcNow.AddMilliseconds(-_random.Next(0, 5000)),
                    Value = Math.Round(_random.NextDouble() * 10000, 4),
                    Unit = Units[metricIndex],
                    MetricType = MetricTypes[metricIndex]
                };
            }

            await Task.Delay(1000 / _options.MessageRateHz, cancellationToken);
        }
    }
}
