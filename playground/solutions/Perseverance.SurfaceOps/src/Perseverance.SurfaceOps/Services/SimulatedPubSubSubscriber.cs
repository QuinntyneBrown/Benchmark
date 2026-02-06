using Perseverance.SurfaceOps.Models;
using Perseverance.SurfaceOps.Configuration;
using Microsoft.Extensions.Options;

namespace Perseverance.SurfaceOps.Services;

public class SimulatedPubSubSubscriber : IPubSubSubscriber
{
    private readonly TelemetryOptions _options;
    private readonly ILogger<SimulatedPubSubSubscriber> _logger;
    private readonly Random _random = new();

    private static readonly string[] Sources =
    {
        "PERCY-MASTCAM-Z", "PERCY-SUPERCAM", "PERCY-PIXL", "PERCY-SHERLOC",
        "PERCY-MOXIE", "PERCY-MEDA", "PERCY-RIMFAX", "INGENUITY-NAV",
        "INGENUITY-ALTIMETER"
    };

    private static readonly string[] MetricTypes =
    {
        "SOIL_TEMPERATURE", "AIR_TEMPERATURE", "WIND_SPEED", "PRESSURE_PA",
        "UV_INDEX", "DUST_OPACITY", "POWER_GENERATION_W", "BATTERY_SOC",
        "WHEEL_CURRENT_A", "ROTOR_RPM"
    };

    private static readonly string[] Units =
    { "°C", "°C", "m/s", "Pa", "index", "tau", "W", "%", "A", "rpm" };

    public SimulatedPubSubSubscriber(IOptions<TelemetryOptions> options, ILogger<SimulatedPubSubSubscriber> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<TelemetryMessage> SubscribeAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Perseverance surface ops subscribing to {Topic}", topic);

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
                    Value = Math.Round(_random.NextDouble() * 500, 4),
                    Unit = Units[metricIndex],
                    MetricType = MetricTypes[metricIndex]
                };
            }

            await Task.Delay(1000 / _options.MessageRateHz, cancellationToken);
        }
    }
}
