using System.Text.Json;
using System.Threading.Channels;
using Juno.RadiationMonitor.Configuration;
using Juno.RadiationMonitor.Hubs;
using Juno.RadiationMonitor.Models;
using Juno.RadiationMonitor.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace Juno.RadiationMonitor;

public class Worker : BackgroundService
{
    private readonly IPubSubSubscriber _subscriber;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly IHubContext<EventHub> _eventHubContext;
    private readonly TelemetryOptions _options;
    private readonly ILogger<Worker> _logger;
    private readonly ObjectPool<TelemetryMessage[]> _arrayPool;
    private readonly long _delayThresholdTicks;
    private long _droppedMessages;

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
        _arrayPool = new DefaultObjectPool<TelemetryMessage[]>(new ArrayPoolPolicy(_options.PoolArraySize));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Juno Radiation Monitor starting (channel: {Capacity}, batch: {BatchSize}, pool array: {PoolSize})",
            _options.ChannelCapacity, _options.DispatchBatchSize, _options.PoolArraySize);

        var channel = Channel.CreateBounded<RadiationReading>(
            new BoundedChannelOptions(_options.ChannelCapacity)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });

        var producerTask = ProduceAsync(channel.Writer, stoppingToken);
        var consumerTask = ConsumeAsync(channel.Reader, stoppingToken);

        await Task.WhenAll(producerTask, consumerTask);
    }

    private async Task ProduceAsync(ChannelWriter<RadiationReading> writer, CancellationToken ct)
    {
        try
        {
            await foreach (var reading in _subscriber.SubscribeAsync("radiation-telemetry", ct))
            {
                // Tag delay using ticks comparison (no DateTime allocation)
                var nowTicks = DateTime.UtcNow.Ticks;
                var ageTicks = nowTicks - reading.TimestampTicks;

                if (ageTicks > _delayThresholdTicks)
                {
                    // Create new struct with IsDelayed set (structs are immutable by convention)
                    var delayed = new RadiationReading
                    {
                        TimestampTicks = reading.TimestampTicks,
                        Value = reading.Value,
                        SourceIndex = reading.SourceIndex,
                        MetricIndex = reading.MetricIndex,
                        IsDelayed = true
                    };

                    if (!writer.TryWrite(delayed))
                    {
                        Interlocked.Increment(ref _droppedMessages);
                    }
                }
                else
                {
                    if (!writer.TryWrite(reading))
                    {
                        Interlocked.Increment(ref _droppedMessages);
                    }
                }
            }
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task ConsumeAsync(ChannelReader<RadiationReading> reader, CancellationToken ct)
    {
        var batchArray = _arrayPool.Get();
        int batchIndex = 0;

        try
        {
            await foreach (var reading in reader.ReadAllAsync(ct))
            {
                // Convert struct to class for serialization
                batchArray[batchIndex++] = new TelemetryMessage
                {
                    MessageId = string.Empty, // Avoid Guid allocation in hot path
                    Source = SimulatedPubSubSubscriber.Sources[reading.SourceIndex],
                    Timestamp = new DateTime(reading.TimestampTicks, DateTimeKind.Utc),
                    Value = reading.Value,
                    Unit = SimulatedPubSubSubscriber.Units[reading.MetricIndex],
                    MetricType = SimulatedPubSubSubscriber.MetricTypes[reading.MetricIndex],
                    IsDelayed = reading.IsDelayed
                };

                if (batchIndex >= _options.DispatchBatchSize)
                {
                    await DispatchBatchAsync(batchArray, batchIndex, ct);
                    _arrayPool.Return(batchArray);
                    batchArray = _arrayPool.Get();
                    batchIndex = 0;
                }
            }

            // Flush remaining
            if (batchIndex > 0)
            {
                await DispatchBatchAsync(batchArray, batchIndex, ct);
            }
        }
        finally
        {
            _arrayPool.Return(batchArray);
        }
    }

    private async Task DispatchBatchAsync(TelemetryMessage[] batch, int count, CancellationToken ct)
    {
        var slice = batch.AsSpan(0, count).ToArray();
        var json = JsonSerializer.Serialize(slice, TelemetryJsonContext.Default.TelemetryMessageArray);

        await _hubContext.Clients.Group("radiation-telemetry")
            .SendAsync("ReceiveTelemetryBatch", json, ct);

        var delayedCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (batch[i].IsDelayed) delayedCount++;
        }

        if (delayedCount > 0 || Interlocked.Read(ref _droppedMessages) > 0)
        {
            var evt = new RadiationBatchEvent
            {
                BatchSize = count,
                DelayedCount = delayedCount,
                TimestampTicks = DateTime.UtcNow.Ticks,
                DroppedTotal = _droppedMessages
            };

            await _eventHubContext.Clients.Group("events-radiation")
                .SendAsync("RadiationEvent",
                    JsonSerializer.Serialize(evt, TelemetryJsonContext.Default.RadiationBatchEvent), ct);
        }

        _logger.LogDebug("Dispatched radiation batch of {Count} readings", count);
    }

    private sealed class ArrayPoolPolicy : PooledObjectPolicy<TelemetryMessage[]>
    {
        private readonly int _size;
        public ArrayPoolPolicy(int size) => _size = size;
        public override TelemetryMessage[] Create() => new TelemetryMessage[_size];
        public override bool Return(TelemetryMessage[] obj)
        {
            Array.Clear(obj);
            return true;
        }
    }
}
