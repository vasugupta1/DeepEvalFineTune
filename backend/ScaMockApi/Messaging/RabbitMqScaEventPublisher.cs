using System.Text.Json;
using RabbitMQ.Client;
using ScaMockApi.Models;

namespace ScaMockApi.Messaging;

public class RabbitMqScaEventPublisher : IScaEventPublisher
{
    private const string QueueName = "sca-transaction-events";
    private readonly IConnection _connection;

    public RabbitMqScaEventPublisher(IConnection connection)
    {
        _connection = connection;
    }

    public async Task PublishResponseAsync<TPayload>(
        string eventType,
        string endpoint,
        int statusCode,
        TPayload response,
        CancellationToken cancellationToken = default)
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var envelope = new ScaResponseEvent<TPayload>(
            EventId: Guid.NewGuid(),
            EventType: eventType,
            Endpoint: endpoint,
            StatusCode: statusCode,
            Response: response,
            EmittedAt: DateTimeOffset.UtcNow
        );

        var body = JsonSerializer.SerializeToUtf8Bytes(envelope);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = envelope.EventId.ToString(),
            Type = eventType
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: QueueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
