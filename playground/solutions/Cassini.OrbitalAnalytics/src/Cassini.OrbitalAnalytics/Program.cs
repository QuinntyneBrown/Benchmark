using Cassini.OrbitalAnalytics;
using Cassini.OrbitalAnalytics.Configuration;
using Cassini.OrbitalAnalytics.Hubs;
using Cassini.OrbitalAnalytics.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TelemetryOptions>(
    builder.Configuration.GetSection(TelemetryOptions.SectionName));

builder.Services.AddSignalR();
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = builder.Configuration.GetValue<int>("Telemetry:CacheSizeLimit");
});
builder.Services.AddSingleton<IPubSubSubscriber, SimulatedPubSubSubscriber>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapHub<TelemetryHub>("/hubs/telemetry");
app.MapHub<EventHub>("/hubs/events");

app.Run();
