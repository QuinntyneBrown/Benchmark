using SLS.LaunchTelemetry;
using SLS.LaunchTelemetry.Configuration;
using SLS.LaunchTelemetry.Hubs;
using SLS.LaunchTelemetry.Services;

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
