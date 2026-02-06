using Hubble.ImageTelemetry;
using Hubble.ImageTelemetry.Configuration;
using Hubble.ImageTelemetry.Hubs;
using Hubble.ImageTelemetry.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TelemetryOptions>(
    builder.Configuration.GetSection(TelemetryOptions.SectionName));

builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IPubSubSubscriber, SimulatedPubSubSubscriber>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapHub<TelemetryHub>("/hubs/telemetry");

app.Run();
