using Apollo.SignalProcessor.Models;
using Apollo.SignalProcessor.Configuration;
using Microsoft.Extensions.Options;

namespace Apollo.SignalProcessor.Services;

public class SimulatedPubSubSubscriber : IPubSubSubscriber
{
    private readonly TelemetryOptions _options;
    private readonly ILogger<SimulatedPubSubSubscriber> _logger;
    private readonly Random _random = new();

    private static readonly string[] Sources =
    {
        "APOLLO-CSM", "APOLLO-LM", "HOUSTON-CAPCOM", "MSFN-GOLDSTONE",
        "MSFN-MADRID", "MSFN-CANBERRA", "S-BAND-TRANSPONDER",
        "VHF-RELAY", "USB-ANTENNA"
    };

    private static readonly string[] MetricTypes =
    {
        "SIGNAL_STRENGTH", "BIT_ERROR_RATE", "AGC_VOLTAGE", "CARRIER_FREQ_HZ",
        "SUBCARRIER_FREQ", "MODULATION_INDEX", "DOPPLER_SHIFT",
        "RANGE_KM", "RANGE_RATE_MPS", "LOCK_STATUS"
    };

    private static readonly string[] Units =
    { "dBm", "ratio", "V", "Hz", "Hz", "rad", "Hz", "km", "m/s", "bool" };

    public SimulatedPubSubSubscriber(IOptions<TelemetryOptions> options, ILogger<SimulatedPubSubSubscriber> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<TelemetryMessage> SubscribeAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Apollo signal processor subscribing to {Topic}", topic);

        while (!cancellationToken.IsCancellationRequested)
        {
            var messageCount = _random.Next(50, 200);

            for (int i = 0; i < messageCount; i++)
            {
                var metricIndex = _random.Next(MetricTypes.Length);

                yield return new TelemetryMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Source = Sources[_random.Next(Sources.Length)],
                    Timestamp = DateTime.UtcNow.AddMilliseconds(-_random.Next(0, 5000)),
                    Value = Math.Round(_random.NextDouble() * 1000, 4),
                    Unit = Units[metricIndex],
                    MetricType = MetricTypes[metricIndex]
                };
            }

            await Task.Delay(1000 / _options.MessageRateHz, cancellationToken);
        }
    }
}
