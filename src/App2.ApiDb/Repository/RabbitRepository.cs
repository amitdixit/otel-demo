using App2.ApiDb.Events;
using Microsoft.Data.SqlClient;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace App2.ApiDb.Repository;

public class RabbitRepository : IRabbitRepository
{
    private static readonly ActivitySource Activity = new(nameof(RabbitRepository));
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private readonly ILogger<RabbitRepository> _logger;
    private readonly IConfiguration _configuration;

    public RabbitRepository(
            ILogger<RabbitRepository> logger,
            IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public void Publish(IEvent evt)
    {
        try
        {
            using (var activity = Activity.StartActivity("RabbitMq Publish", ActivityKind.Producer))
            {
                var factory = new ConnectionFactory { HostName = _configuration["RabbitMq:Host"] };
                using (var connection = factory.CreateConnection())
                using (var channel = connection.CreateModel())
                {
                    var props = channel.CreateBasicProperties();

                    AddActivityToHeader(activity, props);

                    channel.QueueDeclare(queue: "sample_2",
                        durable: false,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));
                    _logger.LogInformation("Publishing message to queue");

                    channel.BasicPublish(exchange: "",
                        routingKey: "sample_2",
                        basicProperties: props,
                        body: body);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error trying to publish a message");
            throw;
        }

    }

    private void AddActivityToHeader(Activity activity, IBasicProperties props)
    {
        Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), props, InjectContextIntoHeader);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination_kind", "queue");
        activity?.SetTag("messaging.rabbitmq.queue", "sample_2");
    }

    private void InjectContextIntoHeader(IBasicProperties props, string key, string value)
    {
        try
        {
            props.Headers ??= new Dictionary<string, object>();
            props.Headers[key] = value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject trace context.");
        }
    }
}

public class SqlRepository : ISqlRepository
{
    private const string Query = "SELECT GETDATE()";
    private readonly IConfiguration _configuration;

    public SqlRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task Persist(string message)
    {
        await using var conn = new SqlConnection(_configuration["SqlDbConnString"]);
        await conn.OpenAsync();

        //Do something more complex
        await using var cmd = new SqlCommand(Query, conn);
        var res = await cmd.ExecuteScalarAsync();
    }
}