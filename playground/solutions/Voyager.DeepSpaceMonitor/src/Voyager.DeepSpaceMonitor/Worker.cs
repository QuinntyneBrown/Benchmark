using System.Text.Json;
using Voyager.DeepSpaceMonitor.Configuration;
using Voyager.DeepSpaceMonitor.Hubs;
using Voyager.DeepSpaceMonitor.Models;
using Voyager.DeepSpaceMonitor.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Voyager.DeepSpaceMonitor;

public class Worker : BackgroundService
{
    private readonly IPubSubSubscriber _subscriber;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly IHubContext<EventHub> _eventHubContext;
    private readonly IMemoryCache _cache;
    private readonly TelemetryOptions _options;
    private readonly ILogger<Worker> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Voyager Deep Space Monitor starting");

        await foreach (var message in _subscriber.SubscribeAsync("deep-space-telemetry", stoppingToken))
        {
            // Tag delay
            var age = DateTime.UtcNow - message.Timestamp;
            if (age.TotalMilliseconds > _options.DelayThresholdMs)
            {
                message.IsDelayed = true;
            }

            // Decompose and process sub-messages in parallel using LINQ GroupBy
            var groupedSubs = message.SubMessages
                .GroupBy(s => s.Category)
                .ToList();

            var tasks = groupedSubs.Select(async group =>
            {
                var categoryData = new
                {
                    ParentId = message.MessageId,
                    Category = group.Key,
                    Readings = group.ToList(),
                    message.Source,
                    message.Timestamp,
                    message.IsDelayed
                };

                var json = JsonSerializer.Serialize(categoryData, JsonOptions);

                await _hubContext.Clients.Group("deep-space-telemetry")
                    .SendAsync("ReceiveSubTelemetry", json, stoppingToken);
            });

            await Task.WhenAll(tasks);

            // Send summary to EventHub
            _cache.Set(message.MessageId, message, TimeSpan.FromMinutes(1));

            var summaryJson = JsonSerializer.Serialize(new
            {
                message.MessageId,
                message.Source,
                message.MetricType,
                message.Value,
                SubMessageCount = message.SubMessages.Count,
                message.IsDelayed
            }, JsonOptions);

            await _eventHubContext.Clients.Group("events-telemetry")
                .SendAsync("TelemetryEvent", summaryJson, stoppingToken);
        }
    }
}
