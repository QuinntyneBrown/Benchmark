using Microsoft.AspNetCore.SignalR;

namespace Voyager.DeepSpaceMonitor.Hubs;

public class TelemetryHub : Hub
{
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(ILogger<TelemetryHub> logger)
    {
        _logger = logger;
    }

    public async Task Subscribe(string topic)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, topic);
        _logger.LogInformation("Client {ConnectionId} subscribed to {Topic}", Context.ConnectionId, topic);
    }

    public async Task Unsubscribe(string topic)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, topic);
    }
}
