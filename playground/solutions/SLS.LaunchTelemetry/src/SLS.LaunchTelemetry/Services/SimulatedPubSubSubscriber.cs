using SLS.LaunchTelemetry.Models;
using SLS.LaunchTelemetry.Configuration;
using Microsoft.Extensions.Options;

namespace SLS.LaunchTelemetry.Services;

public class SimulatedPubSubSubscriber : IPubSubSubscriber
{
    private readonly TelemetryOptions _options;
    private readonly ILogger<SimulatedPubSubSubscriber> _logger;
    private readonly Random _random = new();

    public static readonly string[] Sources =
    {
        "SLS-CORE-RS25-1", "SLS-CORE-RS25-2", "SLS-CORE-RS25-3", "SLS-CORE-RS25-4",
        "SLS-SRB-LEFT", "SLS-SRB-RIGHT", "SLS-ICPS", "SLS-LVSA",
        "SLS-ORION-SA"
    };

    public static readonly string[] MetricTypes =
    {
        "CHAMBER_PRESSURE_PSI", "TURBOPUMP_RPM", "THRUST_KN", "FUEL_FLOW_KGS",
        "LOX_FLOW_KGS", "NOZZLE_TEMP_C", "VIBRATION_G", "ACCELERATION_G",
        "ALTITUDE_M", "VELOCITY_MS"
    };

    public static readonly string[] Units =
    { "psi", "rpm", "kN", "kg/s", "kg/s", "\u00b0C", "g", "g", "m", "m/s" };

    public SimulatedPubSubSubscriber(IOptions<TelemetryOptions> options, ILogger<SimulatedPubSubSubscriber> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<LaunchReading> SubscribeAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("SLS launch telemetry subscribing to {Topic}", topic);

        while (!cancellationToken.IsCancellationRequested)
        {
            var messageCount = _random.Next(50, 200);

            for (int i = 0; i < messageCount; i++)
            {
                yield return new LaunchReading
                {
                    TimestampTicks = DateTime.UtcNow.AddMilliseconds(-_random.Next(0, 5000)).Ticks,
                    Value = _random.NextDouble() * 10000,
                    SourceIndex = (byte)_random.Next(Sources.Length),
                    MetricIndex = (byte)_random.Next(MetricTypes.Length),
                    Flags = TelemetryFlags.None
                };
            }

            await Task.Delay(1000 / _options.MessageRateHz, cancellationToken);
        }
    }
}
