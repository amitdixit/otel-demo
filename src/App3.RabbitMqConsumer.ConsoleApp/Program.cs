using App.RabbitMqConsumer.ConsoleApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(builder =>
    {
        builder.AddEnvironmentVariables();
        builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);



        //var config = builder.Build();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        services.AddOpenTelemetry().WithTracing(builder =>
        {
            builder.AddHttpClientInstrumentation()
                .AddSource("App3.RabbitMqConsumer.ConsoleApp")
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("App3"))
                .AddOtlpExporter(opts =>
                {
                    opts.Endpoint =
                        new Uri(
                            $"{configuration["Jaeger:Protocol"]}://{configuration["Jaeger:Host"]}:{configuration["Jaeger:Port"]}");
                });
        });

        services.AddSingleton<StartupApplication>();
        services.AddSingleton<IDemoService, DemoService>();
    })
    .Build();


await host.Services.GetRequiredService<StartupApplication>().RunAsync();

//Task completes when the host shuts down
await host.RunAsync();