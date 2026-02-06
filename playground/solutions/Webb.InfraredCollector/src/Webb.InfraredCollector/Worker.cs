using System.Text.Json;
using System.Threading.Channels;
using Webb.InfraredCollector.Configuration;
using Webb.InfraredCollector.Hubs;
using Webb.InfraredCollector.Models;
using Webb.InfraredCollector.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Webb.InfraredCollector;

public class Worker : BackgroundService
{
    private readonly IPubSubSubscriber _subscriber;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly IHubContext<EventHub> _eventHubContext;
    private readonly TelemetryOptions _options;
    private readonly ILogger<Worker> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Worker(
        IPubSubSubscriber subscriber,
        IHubContext<TelemetryHub> hubContext,
        IHubContext<EventHub> eventHubContext,
        IOptions<TelemetryOptions> options,
        ILogger<Worker> logger)
    {
        _subscriber = subscriber;
        _hubContext = hubContext;
        _eventHubContext = eventHubContext;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webb IR Collector starting 4-stage pipeline (capacity: {Capacity}, dispatch batch: {BatchSize})",
            _options.ChannelCapacity, _options.DispatchBatchSize);

        // Stage channels with bounded capacity and single reader/writer hints
        var ingestToTransform = Channel.CreateBounded<TelemetryMessage>(
            new BoundedChannelOptions(_options.ChannelCapacity)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });

        var transformToTag = Channel.CreateBounded<TelemetryMessage>(
            new BoundedChannelOptions(_options.ChannelCapacity)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });

        var tagToDispatch = Channel.CreateBounded<TelemetryMessage>(
            new BoundedChannelOptions(_options.ChannelCapacity)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });

        // Launch all 4 stages concurrently
        var ingestTask = IngestStageAsync(ingestToTransform.Writer, stoppingToken);
        var transformTask = TransformStageAsync(ingestToTransform.Reader, transformToTag.Writer, stoppingToken);
        var tagTask = TagStageAsync(transformToTag.Reader, tagToDispatch.Writer, stoppingToken);
        var dispatchTask = DispatchStageAsync(tagToDispatch.Reader, stoppingToken);

        await Task.WhenAll(ingestTask, transformTask, tagTask, dispatchTask);
    }

    private async Task IngestStageAsync(ChannelWriter<TelemetryMessage> writer, CancellationToken ct)
    {
        try
        {
            await foreach (var message in _subscriber.SubscribeAsync("ir-telemetry", ct))
            {
                message.ProcessingStage = "Ingested";
                await writer.WriteAsync(message, ct);
            }
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task TransformStageAsync(
        ChannelReader<TelemetryMessage> reader,
        ChannelWriter<TelemetryMessage> writer,
        CancellationToken ct)
    {
        try
        {
            await foreach (var message in reader.ReadAllAsync(ct))
            {
                // IR calibration transform
                message.TransformedValue = message.MetricType switch
                {
                    "IR_FLUX_JY" => message.Value * 1e-26,     // Convert Jansky to W/m²/Hz
                    "WAVELENGTH_UM" => message.Value * 1e-6,    // Convert μm to m
                    "DETECTOR_TEMP_K" => message.Value - 273.15, // K to °C for display
                    _ => message.Value
                };
                message.ProcessingStage = "Transformed";
                await writer.WriteAsync(message, ct);
            }
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task TagStageAsync(
        ChannelReader<TelemetryMessage> reader,
        ChannelWriter<TelemetryMessage> writer,
        CancellationToken ct)
    {
        try
        {
            await foreach (var message in reader.ReadAllAsync(ct))
            {
                var age = DateTime.UtcNow - message.Timestamp;
                if (age.TotalMilliseconds > _options.DelayThresholdMs)
                {
                    message.IsDelayed = true;
                }
                message.ProcessingStage = "Tagged";
                await writer.WriteAsync(message, ct);
            }
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task DispatchStageAsync(ChannelReader<TelemetryMessage> reader, CancellationToken ct)
    {
        var batch = new List<TelemetryMessage>(_options.DispatchBatchSize);

        await foreach (var message in reader.ReadAllAsync(ct))
        {
            message.ProcessingStage = "Dispatched";
            batch.Add(message);

            if (batch.Count >= _options.DispatchBatchSize)
            {
                await SendBatchAsync(batch, ct);
                batch.Clear();
            }
        }

        // Flush remaining
        if (batch.Count > 0)
        {
            await SendBatchAsync(batch, ct);
        }
    }

    private async Task SendBatchAsync(List<TelemetryMessage> batch, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(batch, JsonOptions);

        await _hubContext.Clients.Group("ir-telemetry")
            .SendAsync("ReceiveTelemetryBatch", json, ct);

        // Event notification for delayed messages
        var delayedCount = batch.Count(m => m.IsDelayed);
        if (delayedCount > 0)
        {
            await _eventHubContext.Clients.Group("events-pipeline")
                .SendAsync("PipelineEvent", JsonSerializer.Serialize(new
                {
                    Stage = "Dispatch",
                    BatchSize = batch.Count,
                    DelayedCount = delayedCount,
                    Timestamp = DateTime.UtcNow
                }, JsonOptions), ct);
        }

        _logger.LogDebug("Dispatched IR batch of {Count} readings", batch.Count);
    }
}
