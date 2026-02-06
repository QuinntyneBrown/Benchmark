using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text.Json;
using SLS.LaunchTelemetry.Configuration;
using SLS.LaunchTelemetry.Hubs;
using SLS.LaunchTelemetry.Models;
using SLS.LaunchTelemetry.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace SLS.LaunchTelemetry;

public class Worker : BackgroundService
{
    private readonly IPubSubSubscriber _subscriber;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly IHubContext<EventHub> _eventHubContext;
    private readonly TelemetryOptions _options;
    private readonly ILogger<Worker> _logger;
    private readonly long _delayThresholdTicks;
    private long _totalProcessed;
    private long _totalDelayed;

    // UTF-8 property name bytes cached for Utf8JsonWriter
    private static readonly byte[] PropSource = "src"u8.ToArray();
    private static readonly byte[] PropTimestamp = "ts"u8.ToArray();
    private static readonly byte[] PropValue = "v"u8.ToArray();
    private static readonly byte[] PropMetric = "m"u8.ToArray();
    private static readonly byte[] PropUnit = "u"u8.ToArray();
    private static readonly byte[] PropDelayed = "d"u8.ToArray();

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
        _delayThresholdTicks = TimeSpan.FromMilliseconds(_options.DelayThresholdMs).Ticks;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SLS Launch Telemetry starting (pipe buffer: {Buffer}, batch: {Batch}, pool max: {Pool})",
            _options.PipeBufferSize, _options.DispatchBatchSize, _options.ArrayPoolMaxSize);

        var queue = new ConcurrentQueue<LaunchReading>();

        var producerTask = ProduceAsync(queue, stoppingToken);
        var consumerTask = ConsumeAsync(queue, stoppingToken);

        await Task.WhenAll(producerTask, consumerTask);
    }

    private async Task ProduceAsync(ConcurrentQueue<LaunchReading> queue, CancellationToken ct)
    {
        await foreach (var reading in _subscriber.SubscribeAsync("launch-telemetry", ct))
        {
            // Tag delay using ticks - zero alloc
            var nowTicks = DateTime.UtcNow.Ticks;
            var ageTicks = nowTicks - reading.TimestampTicks;

            if (ageTicks > _delayThresholdTicks)
            {
                var delayed = reading;
                delayed.Flags |= TelemetryFlags.IsDelayed;
                queue.Enqueue(delayed);
            }
            else
            {
                queue.Enqueue(reading);
            }
        }
    }

    private async Task ConsumeAsync(ConcurrentQueue<LaunchReading> queue, CancellationToken ct)
    {
        // Pre-allocate pinned array for batch processing
        var batchBuffer = GC.AllocateArray<LaunchReading>(_options.DispatchBatchSize, pinned: true);

        while (!ct.IsCancellationRequested)
        {
            int count = 0;

            // Drain from ConcurrentQueue into batch buffer
            while (count < _options.DispatchBatchSize && queue.TryDequeue(out var reading))
            {
                batchBuffer[count++] = reading;
            }

            if (count > 0)
            {
                await DispatchBatchAsync(batchBuffer, count, ct);
                Interlocked.Add(ref _totalProcessed, count);
            }
            else
            {
                // Brief yield when nothing to process
                await Task.Delay(1, ct);
            }
        }
    }

    private async Task DispatchBatchAsync(LaunchReading[] batch, int count, CancellationToken ct)
    {
        // Use ArrayPool for the JSON output buffer
        var buffer = ArrayPool<byte>.Shared.Rent(_options.ArrayPoolMaxSize);

        try
        {
            using var stream = new MemoryStream(buffer);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { SkipValidation = true });

            writer.WriteStartArray();

            var delayedCount = 0;

            // Read batch as a span for zero-copy iteration
            var span = batch.AsSpan(0, count);

            for (int i = 0; i < span.Length; i++)
            {
                ref readonly var reading = ref span[i];

                writer.WriteStartObject();
                writer.WriteString(PropSource, SimulatedPubSubSubscriber.Sources[reading.SourceIndex]);
                writer.WriteNumber(PropTimestamp, reading.TimestampTicks);
                writer.WriteNumber(PropValue, reading.Value);
                writer.WriteString(PropMetric, SimulatedPubSubSubscriber.MetricTypes[reading.MetricIndex]);
                writer.WriteString(PropUnit, SimulatedPubSubSubscriber.Units[reading.MetricIndex]);

                if (reading.IsDelayed)
                {
                    writer.WriteBoolean(PropDelayed, true);
                    delayedCount++;
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.Flush();

            var json = System.Text.Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Position);

            await _hubContext.Clients.Group("launch-telemetry")
                .SendAsync("ReceiveTelemetryBatch", json, ct);

            if (delayedCount > 0)
            {
                Interlocked.Add(ref _totalDelayed, delayedCount);

                await _eventHubContext.Clients.Group("events-launch")
                    .SendAsync("LaunchEvent", JsonSerializer.Serialize(new
                    {
                        BatchSize = count,
                        DelayedCount = delayedCount,
                        TotalProcessed = _totalProcessed,
                        TotalDelayed = _totalDelayed,
                        TimestampTicks = DateTime.UtcNow.Ticks
                    }), ct);
            }

            _logger.LogDebug("Dispatched launch batch: {Count} readings ({Delayed} delayed)", count, delayedCount);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
