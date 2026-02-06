using Hubble.ImageTelemetry.Models;
using Hubble.ImageTelemetry.Configuration;
using Microsoft.Extensions.Options;

namespace Hubble.ImageTelemetry.Services;

public class SimulatedPubSubSubscriber : IPubSubSubscriber
{
    private readonly TelemetryOptions _options;
    private readonly ILogger<SimulatedPubSubSubscriber> _logger;
    private readonly Random _random = new();

    private static readonly string[] Sources =
    {
        "HST-WFC3-UVIS", "HST-WFC3-IR", "HST-ACS-WFC", "HST-ACS-HRC",
        "HST-COS-FUV", "HST-COS-NUV", "HST-STIS-CCD", "HST-STIS-MAMA",
        "HST-FGS-1", "HST-FGS-2"
    };

    private static readonly string[] MetricTypes =
    {
        "CCD_TEMPERATURE", "EXPOSURE_TIME", "PHOTON_COUNT", "DARK_CURRENT",
        "READ_NOISE", "FLAT_FIELD_RATIO", "POINTING_ERROR_ARCSEC",
        "GUIDE_STAR_LOCK", "SHUTTER_STATUS", "FILTER_WHEEL_POS"
    };

    private static readonly string[] Units =
    { "°C", "s", "counts", "e-/s", "e-", "ratio", "arcsec", "bool", "enum", "pos" };

    private static readonly string[] StreamIds =
    { "STREAM-VIS", "STREAM-IR", "STREAM-UV", "STREAM-GUIDE" };

    public SimulatedPubSubSubscriber(IOptions<TelemetryOptions> options, ILogger<SimulatedPubSubSubscriber> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<TelemetryMessage> SubscribeAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Hubble image telemetry subscribing to {Topic}", topic);
        int frameCounter = 0;

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
                    MetricType = MetricTypes[metricIndex],
                    StreamId = StreamIds[_random.Next(StreamIds.Length)],
                    FrameSequence = frameCounter++ / _options.FrameSize
                };
            }

            await Task.Delay(1000 / _options.MessageRateHz, cancellationToken);
        }
    }
}
