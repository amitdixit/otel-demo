using App.RabbitMqConsumer.Worker;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddFusionCache()
    .WithSerializer(new FusionCacheSystemTextJsonSerializer())
    .WithBackplane(
        new RedisBackplane(new RedisBackplaneOptions { Configuration = $"{builder.Configuration["Redis:Host"]}:{builder.Configuration["Redis:Port"]}" })
    );

builder.Services.AddOpenTelemetry().WithTracing(b =>
{
    b.AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddFusionCacheInstrumentation()
        .AddSource(nameof(Worker))
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("App4"))
        .AddOtlpExporter(opts =>
        {
            opts.Endpoint =
                new Uri(
                    $"{builder.Configuration["Jaeger:Protocol"]}://{builder.Configuration["Jaeger:Host"]}:{builder.Configuration["Jaeger:Port"]}");
        });
});

var host = builder.Build();
host.Run();