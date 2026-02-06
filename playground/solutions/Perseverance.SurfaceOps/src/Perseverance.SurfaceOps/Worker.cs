using System.Text.Json;
using Perseverance.SurfaceOps.Configuration;
using Perseverance.SurfaceOps.Hubs;
using Perseverance.SurfaceOps.Models;
using Perseverance.SurfaceOps.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Perseverance.SurfaceOps;

public class Worker : BackgroundService
{
    private readonly IPubSubSubscriber _subscriber;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly IHubContext<EventHub> _eventHubContext;
    private readonly IMemoryCache _cache;
    private readonly TelemetryOptions _options;
    private readonly ILogger<Worker> _logger;
    private readonly SemaphoreSlim _throttle;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
    };

    public Worker(
        IPubSubSubscriber subscriber,
        IHubContext<TelemetryHub> hubContext,
        IHubContext<EventHub> eventHubContext,
        IMemoryCache cache,
        IOptions<TelemetryOptions> options,
        ILogger<Worker> logger)
    {
        _subscriber = subscriber;
        _hubContext = hubContext;
        _eventHubContext = eventHubContext;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
        _throttle = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Perseverance Surface Ops starting with batch window {WindowMs}ms, max batch {MaxBatch}, concurrency {Concurrency}",
            _options.BatchWindowMs, _options.MaxBatchSize, _options.MaxConcurrency);

        var batch = new List<TelemetryMessage>(_options.MaxBatchSize);
        var windowStart = DateTime.UtcNow;

        await foreach (var message in _subscriber.SubscribeAsync("surface-ops-telemetry", stoppingToken))
        {
            // Tag delay immediately
            var age = DateTime.UtcNow - message.Timestamp;
            if (age.TotalMilliseconds > _options.DelayThresholdMs)
            {
                message.IsDelayed = true;
            }

            // Cache for dedup/lookup
            _cache.Set(message.MessageId, message, TimeSpan.FromMinutes(2));

            batch.Add(message);

            var windowElapsed = (DateTime.UtcNow - windowStart).TotalMilliseconds >= _options.BatchWindowMs;
            var batchFull = batch.Count >= _options.MaxBatchSize;

            if (batchFull || windowElapsed)
            {
                if (batch.Count > 0)
                {
                    var batchToSend = new List<TelemetryMessage>(batch);
                    batch.Clear();
                    windowStart = DateTime.UtcNow;

                    // Fire-and-forget with semaphore throttle
                    _ = DispatchBatchAsync(batchToSend, stoppingToken);
                }
            }
        }
    }

    private async Task DispatchBatchAsync(List<TelemetryMessage> batch, CancellationToken ct)
    {
        await _throttle.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(batch, JsonOptions);

            await _hubContext.Clients.Group("surface-ops-telemetry")
                .SendAsync("ReceiveTelemetryBatch", json, ct);

            // Notify EventHub about batch stats
            var delayedCount = batch.Count(m => m.IsDelayed);
            if (delayedCount > 0)
            {
                var eventJson = JsonSerializer.Serialize(new
                {
                    EventType = "BatchProcessed",
                    BatchSize = batch.Count,
                    DelayedCount = delayedCount,
                    Timestamp = DateTime.UtcNow
                }, JsonOptions);

                await _eventHubContext.Clients.Group("events-telemetry")
                    .SendAsync("SurfaceOpsEvent", eventJson, ct);
            }

            _logger.LogDebug("Dispatched batch of {Count} messages ({Delayed} delayed)", batch.Count, delayedCount);
        }
        finally
        {
            _throttle.Release();
        }
    }
}
