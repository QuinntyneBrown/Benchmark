using Webb.InfraredCollector;
using Webb.InfraredCollector.Configuration;
using Webb.InfraredCollector.Hubs;
using Webb.InfraredCollector.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TelemetryOptions>(
    builder.Configuration.GetSection(TelemetryOptions.SectionName));

builder.Services.AddSignalR();
builder.Services.AddSingleton<IPubSubSubscriber, SimulatedPubSubSubscriber>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapHub<TelemetryHub>("/hubs/telemetry");
app.MapHub<EventHub>("/hubs/events");

app.Run();
