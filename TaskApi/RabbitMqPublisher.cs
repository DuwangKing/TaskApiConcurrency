using RabbitMQ.Client;
using System.Collections.Concurrent;
using System.Text.Json;

public interface IRabbitMqPublisher
{
    void PublishTaskCompleted(Guid taskId, string title, DateTimeOffset completedAt, Priority priority);
}

public class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly ConcurrentBag<Task> _activePublishes = new ConcurrentBag<Task>();

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;
        var factory = new ConnectionFactory()
        {
            Uri = new Uri(config["RabbitMq:ConnectionString"] ?? "amqp://guest:guest@localhost:5672")
        };
        _connection = factory.CreateConnection();
    }

    public void PublishTaskCompleted(Guid taskId, string title, DateTimeOffset completedAt, Priority priority)
    {
        var publishTask = Task.Run(() =>
        {
            try
            {
                using var channel = _connection.CreateModel();
                channel.ExchangeDeclare("task.events", ExchangeType.Topic, durable: true);

                var message = new
                {
                    taskId = taskId.ToString(),
                    title,
                    completedAt = completedAt.ToString("O"),
                    priority = priority.ToString()
                };

                var body = JsonSerializer.SerializeToUtf8Bytes(message);
                var props = channel.CreateBasicProperties();
                props.Persistent = true;

                channel.BasicPublish(
                    exchange: "task.events",
                    routingKey: "task.completed",
                    basicProperties: props,
                    body: body);
            }

            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to publish task.completed for TaskId: {TaskId}", taskId);
            }
        });

        _activePublishes.Add(publishTask);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_activePublishes.IsEmpty)
        {
            _logger.LogInformation("Waiting for {Count} RabbitMQ publishes...", _activePublishes.Count);
            await Task.WhenAll(_activePublishes).WaitAsync(TimeSpan.FromSeconds(5));
        }
        
        _connection?.Close(timeout: TimeSpan.FromSeconds(3));
        _connection?.Dispose();
    }
}