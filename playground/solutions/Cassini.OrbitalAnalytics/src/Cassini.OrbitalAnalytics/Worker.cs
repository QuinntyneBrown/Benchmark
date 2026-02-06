using System.Text.Json;
using System.Threading.RateLimiting;
using Cassini.OrbitalAnalytics.Configuration;
using Cassini.OrbitalAnalytics.Hubs;
using Cassini.OrbitalAnalytics.Models;
using Cassini.OrbitalAnalytics.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cassini.OrbitalAnalytics;

public class Worker : BackgroundService
{
    private readonly IPubSubSubscriber _subscriber;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly IHubContext<EventHub> _eventHubContext;
    private readonly IMemoryCache _cache;
    private readonly TelemetryOptions _options;
    private readonly ILogger<Worker> _logger;
    private readonly TokenBucketRateLimiter _rateLimiter;
    private long _droppedMessages;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
        _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = _options.RateLimitPermitsPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = _options.RateLimitPermitsPerSecond,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = _options.RateLimitQueueLimit
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cassini Orbital Analytics starting (cache limit: {CacheLimit}, rate: {Rate}/s)",
            _options.CacheSizeLimit, _options.RateLimitPermitsPerSecond);

        var batch = new List<TelemetryMessage>(_options.BatchSize);

        await foreach (var message in _subscriber.SubscribeAsync("orbital-analytics", stoppingToken))
        {
            // Rate limit check with graceful degradation
            using var lease = _rateLimiter.AttemptAcquire();
            if (!lease.IsAcquired)
            {
                Interlocked.Increment(ref _droppedMessages);

                if (Interlocked.Read(ref _droppedMessages) % 100 == 0)
                {
                    _logger.LogWarning("Rate limited: {Dropped} messages dropped total", _droppedMessages);

                    await _eventHubContext.Clients.Group("events-rate-limit")
                        .SendAsync("RateLimitEvent", JsonSerializer.Serialize(new
                        {
                            DroppedTotal = _droppedMessages,
                            Timestamp = DateTime.UtcNow
                        }, JsonOptions), stoppingToken);
                }
                continue;
            }

            // Tag delay
            var age = DateTime.UtcNow - message.Timestamp;
            if (age.TotalMilliseconds > _options.DelayThresholdMs)
            {
                message.IsDelayed = true;
            }

            // Cache with dual expiration and size limit
            _cache.Set(message.MessageId, message, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.CacheAbsoluteExpirationSeconds),
                SlidingExpiration = TimeSpan.FromSeconds(_options.CacheSlidingExpirationSeconds),
                Size = 1
            });

            batch.Add(message);

            if (batch.Count >= _options.BatchSize)
            {
                var json = JsonSerializer.Serialize(batch, JsonOptions);

                await _hubContext.Clients.Group("orbital-analytics")
                    .SendAsync("ReceiveTelemetryBatch", json, stoppingToken);

                _logger.LogDebug("Sent orbital analytics batch of {Count}", batch.Count);
                batch.Clear();
            }
        }

        _rateLimiter.Dispose();
    }

    public override void Dispose()
    {
        _rateLimiter.Dispose();
        base.Dispose();
    }
}
