using Microsoft.AspNetCore.SignalR;

namespace Artemis.TelemetryRelay.Hubs;

public class TelemetryHub : Hub
{
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(ILogger<TelemetryHub> logger)
    {
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        // Excessive logging - logs every connection event with full detail
        _logger.LogInformation("Client {ConnectionId} connected to TelemetryHub at {Time}",
            Context.ConnectionId, DateTime.UtcNow.ToString("O"));
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected from TelemetryHub at {Time}. Exception: {Exception}",
            Context.ConnectionId, DateTime.UtcNow.ToString("O"), exception?.Message ?? "none");
        return base.OnDisconnectedAsync(exception);
    }

    public Task Subscribe(string topic)
    {
        // Naive: doesn't actually use subscription - always broadcasts to all
        _logger.LogInformation("Client {ConnectionId} subscribed to {Topic} (ignored - broadcasting to all)",
            Context.ConnectionId, topic);
        return Task.CompletedTask;
    }
}
