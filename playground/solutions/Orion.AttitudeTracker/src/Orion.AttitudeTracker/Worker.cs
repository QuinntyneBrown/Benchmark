using System.Text.Json;
using Orion.AttitudeTracker.Configuration;
using Orion.AttitudeTracker.Hubs;
using Orion.AttitudeTracker.Models;
using Orion.AttitudeTracker.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Orion.AttitudeTracker;

public class Worker : BackgroundService
{
    private readonly IPubSubSubscriber _subscriber;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly IMemoryCache _cache;
    private readonly TelemetryOptions _options;
    private readonly ILogger<Worker> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public Worker(
        IPubSubSubscriber subscriber,
        IHubContext<TelemetryHub> hubContext,
        IMemoryCache cache,
        IOptions<TelemetryOptions> options,
        ILogger<Worker> logger)
    {
        _subscriber = subscriber;
        _hubContext = hubContext;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Orion Attitude Tracker starting");

        var batch = new List<TelemetryMessage>(_options.BatchSize);

        await foreach (var message in _subscriber.SubscribeAsync("attitude-telemetry", stoppingToken))
        {
            // Dedup via MemoryCache
            if (_cache.TryGetValue(message.MessageId, out _))
            {
                continue;
            }

            _cache.Set(message.MessageId, true, TimeSpan.FromSeconds(30));

            // Tag delay before batching
            var age = DateTime.UtcNow - message.Timestamp;
            if (age.TotalMilliseconds > _options.DelayThresholdMs)
            {
                message.IsDelayed = true;
            }

            batch.Add(message);

            if (batch.Count >= _options.BatchSize)
            {
                await SendBatchAsync(batch, stoppingToken);
                batch.Clear();
            }
        }

        // Send remaining
        if (batch.Count > 0)
        {
            await SendBatchAsync(batch, stoppingToken);
        }
    }

    private async Task SendBatchAsync(List<TelemetryMessage> batch, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(batch, JsonOptions);

        await _hubContext.Clients.Group("attitude-telemetry")
            .SendAsync("ReceiveTelemetryBatch", json, ct);

        _logger.LogDebug("Sent batch of {Count} attitude readings", batch.Count);
    }
}
