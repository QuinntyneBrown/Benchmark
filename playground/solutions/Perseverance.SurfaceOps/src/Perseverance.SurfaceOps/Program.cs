using Perseverance.SurfaceOps;
using Perseverance.SurfaceOps.Configuration;
using Perseverance.SurfaceOps.Hubs;
using Perseverance.SurfaceOps.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TelemetryOptions>(
    builder.Configuration.GetSection(TelemetryOptions.SectionName));

builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IPubSubSubscriber, SimulatedPubSubSubscriber>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapHub<TelemetryHub>("/hubs/telemetry");
app.MapHub<EventHub>("/hubs/events");

app.Run();
