using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace App.RabbitMqConsumer.ConsoleApp;
internal class StartupApplication
{
    private readonly IDemoService _demoService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StartupApplication> _logger;
    private readonly ActivitySource _activity = new("RabbitMqConsumer.ConsoleApp");
    private readonly TraceContextPropagator _propagator = new();
    public StartupApplication(IDemoService demoService, IConfiguration configuration, ILogger<StartupApplication> logger)
    {
        _demoService = demoService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        await _demoService.PerformSomeTask();

        await Console.Out.WriteLineAsync("Hello World!");
        var factory = new ConnectionFactory() { HostName = _configuration["RabbitMq:Host"], DispatchConsumersAsync = true };

        var rabbitMqConnection = factory.CreateConnection();
        var rabbitMqChannel = rabbitMqConnection.CreateModel();
        var httpClient = new HttpClient { BaseAddress = new Uri(_configuration["App2Endpoint"]) };

        rabbitMqChannel.QueueDeclare(queue: "sample",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        rabbitMqChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(rabbitMqChannel);
        consumer.Received += async (model, ea) =>
        {
            await ProcessMessage(ea,
                httpClient,
                rabbitMqChannel);
        };

        rabbitMqChannel.BasicConsume(queue: "sample",
            autoAck: false,
            consumer: consumer);

        await Console.Out.WriteLineAsync(_configuration.GetConnectionString("SqlConnection"));

        await Task.CompletedTask;
    }


    private async Task ProcessMessage(BasicDeliverEventArgs ea,
            HttpClient httpClient,
            IModel rabbitMqChannel)
    {
        try
        {
            var parentContext = _propagator.Extract(default, ea.BasicProperties, ExtractTraceContextFromBasicProperties);
            Baggage.Current = parentContext.Baggage;

            using (var activity = _activity.StartActivity("Process Message", ActivityKind.Consumer, parentContext.ActivityContext))
            {

                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                AddActivityTags(activity);

                _logger.LogInformation("Message Received: " + message);

                _ = await httpClient.PostAsync("/sql-to-event",
                    new StringContent(JsonSerializer.Serialize(message),
                        Encoding.UTF8,
                        "application/json"));

                rabbitMqChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error processing the message");
        }
    }

    private IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
    {
        try
        {
            if (props.Headers.TryGetValue(key, out var value))
            {
                var bytes = value as byte[];
                return [Encoding.UTF8.GetString(bytes)];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract trace context");
        }

        return Enumerable.Empty<string>();
    }

    private static void AddActivityTags(Activity activity)
    {
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination_kind", "queue");
        activity?.SetTag("messaging.rabbitmq.queue", "sample");
    }
}

public interface IDemoService
{
    Task PerformSomeTask();
}

internal class DemoService : IDemoService
{
    private readonly ILogger<DemoService> _logger;

    public DemoService(ILogger<DemoService> logger)
    {
        _logger = logger;
    }

    public async Task PerformSomeTask()
    {
        await Task.Delay(1000);

        _logger.LogInformation("Performimg Some Task");

    }
}