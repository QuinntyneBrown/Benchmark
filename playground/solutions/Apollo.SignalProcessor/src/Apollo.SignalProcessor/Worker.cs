using System.Text.Json;
using Apollo.SignalProcessor.Configuration;
using Apollo.SignalProcessor.Hubs;
using Apollo.SignalProcessor.Models;
using Apollo.SignalProcessor.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Apollo.SignalProcessor;

public class Worker : BackgroundService
{
    private readonly IPubSubSubscriber _subscriber;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly IMemoryCache _cache;
    private readonly TelemetryOptions _options;
    private readonly ILogger<Worker> _logger;
    private readonly object _lock = new();
    private readonly List<TelemetryMessage> _messageBuffer = new();

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
        _logger.LogInformation("Apollo Signal Processor starting");

        // Start the consumer on a separate task
        var consumerTask = Task.Run(() => ConsumeMessagesAsync(stoppingToken), stoppingToken);

        // Fixed 500ms processing timer
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.ProcessingIntervalMs));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            List<TelemetryMessage> snapshot;

            // Lock-based list access - copies entire list each cycle
            lock (_lock)
            {
                snapshot = new List<TelemetryMessage>(_messageBuffer);
                _messageBuffer.Clear();
            }

            if (snapshot.Count == 0) continue;

            _logger.LogInformation("Processing batch of {Count} signals", snapshot.Count);

            foreach (var message in snapshot)
            {
                // Tag delay
                var age = DateTime.UtcNow - message.Timestamp;
                if (age.TotalMilliseconds > _options.DelayThresholdMs)
                {
                    message.IsDelayed = true;
                }

                // Cache with Guid.ToString() key - unbounded cache, no eviction policy
                _cache.Set(
                    Guid.NewGuid().ToString(),
                    message);

                var json = JsonSerializer.Serialize(message);

                await _hubContext.Clients.Group("apollo-signals")
                    .SendAsync("ReceiveTelemetry", json, stoppingToken);
            }
        }

        await consumerTask;
    }

    private async Task ConsumeMessagesAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _subscriber.SubscribeAsync("apollo-signals", stoppingToken))
        {
            lock (_lock)
            {
                _messageBuffer.Add(message);
            }
        }
    }
}
