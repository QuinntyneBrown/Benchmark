using Webb.InfraredCollector.Models;
using Webb.InfraredCollector.Configuration;
using Microsoft.Extensions.Options;

namespace Webb.InfraredCollector.Services;

public class SimulatedPubSubSubscriber : IPubSubSubscriber
{
    private readonly TelemetryOptions _options;
    private readonly ILogger<SimulatedPubSubSubscriber> _logger;
    private readonly Random _random = new();

    private static readonly string[] Sources =
    {
        "JWST-NIRCAM-A", "JWST-NIRCAM-B", "JWST-NIRSPEC-MSA",
        "JWST-NIRSPEC-IFU", "JWST-MIRI-IMAGER", "JWST-MIRI-MRS",
        "JWST-FGS", "JWST-NIRISS-SOSS", "JWST-NIRISS-AMI"
    };

    private static readonly string[] MetricTypes =
    {
        "IR_FLUX_JY", "WAVELENGTH_UM", "DETECTOR_TEMP_K", "MIRROR_TEMP_K",
        "POINTING_OFFSET_MAS", "DITHER_POS", "EXPOSURE_TIME_S",
        "COSMIC_RAY_HITS", "DARK_CURRENT_EPS", "READ_NOISE_EPS"
    };

    private static readonly string[] Units =
    { "Jy", "\u03bcm", "K", "K", "mas", "pos", "s", "hits", "e-/s", "e-/s" };

    public SimulatedPubSubSubscriber(IOptions<TelemetryOptions> options, ILogger<SimulatedPubSubSubscriber> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<TelemetryMessage> SubscribeAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Webb IR collector subscribing to {Topic}", topic);

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
                    Value = Math.Round(_random.NextDouble() * 1000, 6),
                    Unit = Units[metricIndex],
                    MetricType = MetricTypes[metricIndex],
                    ProcessingStage = "Raw"
                };
            }

            await Task.Delay(1000 / _options.MessageRateHz, cancellationToken);
        }
    }
}
