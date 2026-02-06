using Microsoft.AspNetCore.SignalR;

namespace Webb.InfraredCollector.Hubs;

public class EventHub : Hub
{
    private readonly ILogger<EventHub> _logger;

    public EventHub(ILogger<EventHub> logger)
    {
        _logger = logger;
    }

    public async Task Subscribe(string eventType)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"events-{eventType}");
        _logger.LogInformation("Client {ConnectionId} subscribed to events: {EventType}", Context.ConnectionId, eventType);
    }
}
