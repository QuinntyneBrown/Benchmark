using System.Text.Json;
using Artemis.TelemetryRelay.Configuration;
using Artemis.TelemetryRelay.Hubs;
using Artemis.TelemetryRelay.Models;
using Artemis.TelemetryRelay.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Artemis.TelemetryRelay;

public class Worker : BackgroundService
{
    private readonly IPubSubSubscriber _subscriber;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly TelemetryOptions _options;
    private readonly ILogger<Worker> _logger;
    private long _totalMessagesProcessed;

    public Worker(
        IPubSubSubscriber subscriber,
        IHubContext<TelemetryHub> hubContext,
        IOptions<TelemetryOptions> options,
        ILogger<Worker> logger)
    {
        _subscriber = subscriber;
        _hubContext = hubContext;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Artemis Telemetry Relay Worker starting at {Time}", DateTime.UtcNow);

        await foreach (var message in _subscriber.SubscribeAsync("lunar-telemetry", stoppingToken))
        {
            try
            {
                // Naive: check delay AFTER processing (should be before)
                // Also allocates new JsonSerializerOptions each time
                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Excessive per-message logging
                _logger.LogInformation(
                    "Processing message {MessageId} from {Source}: {MetricType}={Value}{Unit}",
                    message.MessageId, message.Source, message.MetricType, message.Value, message.Unit);

                // Tag delay after serialization (wasteful - should be before)
                var age = DateTime.UtcNow - message.Timestamp;
                if (age.TotalMilliseconds > _options.DelayThresholdMs)
                {
                    message.IsDelayed = true;
                    // Re-serialize because we modified after first serialization
                    json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    _logger.LogWarning("Message {MessageId} is delayed by {DelayMs}ms",
                        message.MessageId, age.TotalMilliseconds);
                }

                // Naive: sends to ALL clients, not just subscribers
                await _hubContext.Clients.All.SendAsync(
                    "ReceiveTelemetry",
                    json,
                    stoppingToken);

                _totalMessagesProcessed++;

                // Excessive: log every single message count
                _logger.LogDebug("Total messages processed: {Count}", _totalMessagesProcessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId}", message.MessageId);
            }
        }
    }
}
