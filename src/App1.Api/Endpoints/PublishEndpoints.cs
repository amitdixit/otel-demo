using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;

namespace App.Api.Endpoints;

internal sealed class PublishEndpoints
{
    private static readonly ActivitySource _activity = new(nameof(PublishEndpoints));
    private static readonly TextMapPropagator _propagator = Propagators.DefaultTextMapPropagator;

    private PublishEndpoints() { } // private constructor


    public static void MapPublishEndpoints(WebApplication app)
    {
        app.MapPost("/publish", Publish);
        app.MapGet("/health", async context =>
        {
            await context.Response.WriteAsync("Ok");
        });

        //app.MapGet("/getusers", GetUsers);
        app.MapGet("/dummy", GetDummy);
    }

    private static async Task<IResult> GetDummy(IHttpClientFactory httpClientFactory, ILogger<PublishEndpoints> logger, IConfiguration configuration)
    {
        logger.LogInformation($"Calling App2: {configuration["App2Endpoint"]}");
        var client = httpClientFactory.CreateClient("app2");

        var response = await client.GetAsync(configuration["App3Endpoint"]);

        if (response.IsSuccessStatusCode)
            return Results.Ok(await response.Content.ReadAsStringAsync());

        return Results.BadRequest("Error from App2");
    }


    private static async Task<IResult> GetUsers(IHttpClientFactory httpClientFactory, ILogger<PublishEndpoints> logger, IConfiguration configuration)
    {
        logger.LogInformation($"Calling App2: {configuration["App2Endpoint"]}");
        var response = await httpClientFactory
            .CreateClient()
            .GetStringAsync(configuration["App3Endpoint"]);

        return Results.Ok(response);
    }

    static Task Publish(PublishRequest request, ILogger<PublishEndpoints> logger, IConfiguration configuration)
    {
        try
        {
            using (var activity = _activity.StartActivity("RabbitMq Publish", ActivityKind.Producer))
            {
                var factory = new ConnectionFactory { HostName = configuration["RabbitMq:Host"] };
                using (var connection = factory.CreateConnection())
                {
                    using (var channel = connection.CreateModel())
                    {
                        var props = channel.CreateBasicProperties();

                        AddActivityToHeader(activity, props);

                        channel.QueueDeclare(queue: "sample",
                            durable: false,
                            exclusive: false,
                            autoDelete: false,
                            arguments: null);

                        var body = Encoding.UTF8.GetBytes($"I am app1 {request.Message}");

                        logger.LogInformation("Publishing message to queue");

                        channel.BasicPublish(exchange: "",
                            routingKey: "sample",
                            basicProperties: props,
                            body: body);
                    }
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError("Error trying to publish a message", e);
            throw;
        }
        return Task.CompletedTask;
    }

    private static void AddActivityToHeader(Activity activity, IBasicProperties props)
    {
        _propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), props, InjectContextIntoHeader);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination_kind", "queue");
        activity?.SetTag("messaging.rabbitmq.queue", "sample");
    }

    private static void InjectContextIntoHeader(IBasicProperties props, string key, string value)
    {
        try
        {
            props.Headers ??= new Dictionary<string, object>();
            props.Headers[key] = value;
        }
        catch (Exception ex)
        {
            // logger.LogError(ex, "Failed to inject trace context.");
        }
    }
}

internal class PublishRequest
{
    public string Message { get; set; }
}