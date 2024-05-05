using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;
using ZiggyCreatures.Caching.Fusion;

namespace App.RabbitMqConsumer.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IFusionCache _cache;
    private readonly IConfiguration _configuration;
    private static readonly ActivitySource Activity = new(nameof(Worker));
    private static readonly TraceContextPropagator Propagator = new();

    public Worker(ILogger<Worker> logger, IFusionCache cache, IConfiguration configuration)
    {
        _logger = logger;
        _cache = cache;
        _configuration = configuration;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);
        StartRabbitConsumer();
        return Task.CompletedTask;
    }

    private void StartRabbitConsumer()
    {
        var factory = new ConnectionFactory() { HostName = _configuration["RabbitMq:Host"], DispatchConsumersAsync = true };
        var rabbitMqConnection = factory.CreateConnection();
        var rabbitMqChannel = rabbitMqConnection.CreateModel();

        rabbitMqChannel.QueueDeclare(queue: "sample_2",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        rabbitMqChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(rabbitMqChannel);
        consumer.Received += async (model, ea) => await ProcessMessage(ea);


        rabbitMqChannel.BasicConsume(queue: "sample_2",
            autoAck: true,
            consumer: consumer);
    }

    private async Task ProcessMessage(BasicDeliverEventArgs ea)
    {
        try
        {
            var parentContext = Propagator.Extract(default,
                ea.BasicProperties,
                ActivityHelper.ExtractTraceContextFromBasicProperties);

            Baggage.Current = parentContext.Baggage;

            using (var activity = Activity.StartActivity("Process Message", ActivityKind.Consumer, parentContext.ActivityContext))
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                ActivityHelper.AddActivityTags(activity);

                _logger.LogInformation("Message Received: " + message);

                var result = await _cache.GetOrDefaultAsync("rabbit.message", string.Empty);

                if (string.IsNullOrEmpty(result))
                {
                    _logger.LogInformation("Add item into redis cache");

                    await _cache.SetAsync("rabbit.message",
                        message,
                        new FusionCacheEntryOptions
                        {
                            Duration = TimeSpan.FromSeconds(30)
                        });
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error processing the message");
        }
    }

}

public static class ActivityHelper
{
    public static IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
    {
        try
        {
            if (props.Headers.TryGetValue(key, out var value))
            {
                var bytes = value as byte[];
                return new[] { Encoding.UTF8.GetString(bytes ?? Array.Empty<byte>()) };
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Failed to extract trace context: {ex}");
        }

        return Enumerable.Empty<string>();
    }

    public static void AddActivityTags(Activity activity)
    {
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination_kind", "queue");
        activity?.SetTag("messaging.rabbitmq.queue", "sample_2");
    }

}