namespace Benchmark.Core.Models;

public class HubEndpointInfo
{
    public string HubClassName { get; set; } = string.Empty;
    public string EndpointPath { get; set; } = string.Empty;
    public string ClientCallbackMethod { get; set; } = string.Empty;
    public string? SubscriptionTopic { get; set; }
    public bool UsesClientsAll => SubscriptionTopic == null;
}
