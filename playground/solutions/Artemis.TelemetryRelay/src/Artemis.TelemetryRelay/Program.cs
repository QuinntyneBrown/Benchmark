using Artemis.TelemetryRelay;
using Artemis.TelemetryRelay.Configuration;
using Artemis.TelemetryRelay.Hubs;
using Artemis.TelemetryRelay.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TelemetryOptions>(
    builder.Configuration.GetSection(TelemetryOptions.SectionName));

builder.Services.AddSignalR();
builder.Services.AddSingleton<IPubSubSubscriber, SimulatedPubSubSubscriber>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapHub<TelemetryHub>("/hubs/telemetry");

app.Run();
