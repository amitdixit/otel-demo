using App2.ApiDb.Events;
using App2.ApiDb.Repository;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;
using System.Text.Json;

namespace App2.ApiDb.Endpoints;

internal sealed class AppEndpoints
{
    private static readonly ActivitySource _activity = new(nameof(AppEndpoints));
    private static readonly TextMapPropagator _propagator = Propagators.DefaultTextMapPropagator;

    private AppEndpoints() { } // private constructor


    public static void MapPublishEndpoints(WebApplication app)
    {
        app.MapGet("/health", async context =>
        {
            await context.Response.WriteAsync("Ok");
        });

        app.MapGet("/dummy", (ILogger<Program> logger) =>
        {
            logger.LogInformation("Logging current activity: {Activity}", JsonSerializer.Serialize(Activity.Current));
            return Results.Ok();
        });

        app.MapPost("/sql-to-event", async (string message,
                                                                        ISqlRepository repository,
                                                                        IRabbitRepository eventPublisher,
                                                                        ILogger<Program> logger) =>
        {
            logger.LogTrace("You call sql save message endpoint");
            if (!string.IsNullOrEmpty(message))
            {
                await repository.Persist(message);
                eventPublisher.Publish(new MessagePersistedEvent { Message = message });
            }
        });

    }


}

