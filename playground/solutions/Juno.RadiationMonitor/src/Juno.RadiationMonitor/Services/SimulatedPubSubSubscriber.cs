using Juno.RadiationMonitor.Models;
using Juno.RadiationMonitor.Configuration;
using Microsoft.Extensions.Options;

namespace Juno.RadiationMonitor.Services;

public class SimulatedPubSubSubscriber : IPubSubSubscriber
{
    private readonly TelemetryOptions _options;
    private readonly ILogger<SimulatedPubSubSubscriber> _logger;
    private readonly Random _random = new();

    public static readonly string[] Sources =
    {
        "JUNO-JEDI-ION", "JUNO-JEDI-ELECTRON", "JUNO-JADE-ION",
        "JUNO-JADE-ELECTRON", "JUNO-UVS", "JUNO-MAG-FGM",
        "JUNO-MAG-ASC", "JUNO-WAVES", "JUNO-GRAVITY"
    };

    public static readonly string[] MetricTypes =
    {
        "RADIATION_RAD", "PARTICLE_ENERGY_MEV", "FLUX_PARTICLES_CM2S",
        "MAGNETIC_FIELD_NT", "UV_INTENSITY", "PLASMA_DENSITY",
        "ELECTRON_TEMP_EV", "ION_VELOCITY_KMS", "GRAVITY_MGA", "DOSE_RATE"
    };

    public static readonly string[] Units =
    { "rad", "MeV", "p/cm\u00b2/s", "nT", "R", "cm\u207b\u00b3", "eV", "km/s", "mGal", "rad/s" };

    public SimulatedPubSubSubscriber(IOptions<TelemetryOptions> options, ILogger<SimulatedPubSubSubscriber> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<RadiationReading> SubscribeAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Juno radiation monitor subscribing to {Topic}", topic);

        while (!cancellationToken.IsCancellationRequested)
        {
            var messageCount = _random.Next(50, 200);

            for (int i = 0; i < messageCount; i++)
            {
                yield return new RadiationReading
                {
                    TimestampTicks = DateTime.UtcNow.AddMilliseconds(-_random.Next(0, 5000)).Ticks,
                    Value = _random.NextDouble() * 1000,
                    SourceIndex = (byte)_random.Next(Sources.Length),
                    MetricIndex = (byte)_random.Next(MetricTypes.Length),
                    IsDelayed = false
                };
            }

            await Task.Delay(1000 / _options.MessageRateHz, cancellationToken);
        }
    }
}
