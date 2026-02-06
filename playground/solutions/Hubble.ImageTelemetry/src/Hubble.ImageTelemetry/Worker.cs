using System.Text.Json;
using Hubble.ImageTelemetry.Configuration;
using Hubble.ImageTelemetry.Hubs;
using Hubble.ImageTelemetry.Models;
using Hubble.ImageTelemetry.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Hubble.ImageTelemetry;

public class Worker : BackgroundService
{
    private readonly IPubSubSubscriber _subscriber;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly IMemoryCache _cache;
    private readonly TelemetryOptions _options;
    private readonly ILogger<Worker> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly Dictionary<string, List<TelemetryMessage>> _streamBuffers = new();
    private int _frameCounter;

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
        _logger.LogInformation("Hubble Image Telemetry starting with {StreamCount} streams", _options.StreamCount);

        await foreach (var message in _subscriber.SubscribeAsync("image-telemetry", stoppingToken))
        {
            // Tag delay
            var age = DateTime.UtcNow - message.Timestamp;
            if (age.TotalMilliseconds > _options.DelayThresholdMs)
            {
                message.IsDelayed = true;
            }

            // Cache with sliding expiration
            _cache.Set(message.MessageId, message, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromSeconds(_options.SlidingExpirationSeconds)
            });

            // Buffer by stream
            if (!_streamBuffers.TryGetValue(message.StreamId, out var buffer))
            {
                buffer = new List<TelemetryMessage>(_options.FrameSize);
                _streamBuffers[message.StreamId] = buffer;
            }

            buffer.Add(message);

            // Check if any streams have enough messages for a frame
            var readyStreams = _streamBuffers
                .Where(kvp => kvp.Value.Count >= _options.FrameSize)
                .ToList();

            if (readyStreams.Count > 0)
            {
                // Compose frames from ready streams in parallel
                var frameTasks = readyStreams.Select(async kvp =>
                {
                    var frame = new TelemetryFrame
                    {
                        FrameId = $"FRAME-{Interlocked.Increment(ref _frameCounter)}",
                        StreamId = kvp.Key,
                        SequenceNumber = _frameCounter,
                        Messages = kvp.Value.Take(_options.FrameSize).ToList(),
                        ComposedAt = DateTime.UtcNow,
                        HasDelayedMessages = kvp.Value.Any(m => m.IsDelayed)
                    };

                    var json = JsonSerializer.Serialize(frame, JsonOptions);

                    await _hubContext.Clients.Group("image-telemetry")
                        .SendAsync("ReceiveFrame", json, stoppingToken);

                    _logger.LogDebug("Composed frame {FrameId} for stream {StreamId} with {Count} messages",
                        frame.FrameId, frame.StreamId, frame.Messages.Count);
                });

                await Task.WhenAll(frameTasks);

                // Clear processed messages from buffers
                foreach (var kvp in readyStreams)
                {
                    var remaining = kvp.Value.Skip(_options.FrameSize).ToList();
                    _streamBuffers[kvp.Key] = remaining;
                }
            }
        }
    }
}
