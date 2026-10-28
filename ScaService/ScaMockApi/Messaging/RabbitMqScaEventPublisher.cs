using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ScaMockApi.Models;
using ScaMockApi.Telemetry;

namespace ScaMockApi.Messaging;

public class RabbitMqScaEventPublisher : IScaEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnection _connection;
    private readonly ScaMetrics _metrics;
    private readonly string _queueName;

    public RabbitMqScaEventPublisher(IConnection connection, IOptions<ScaEventOptions> options, ScaMetrics metrics)
    {
        _connection = connection;
        _metrics = metrics;
        _queueName = options.Value.QueueName;

        if (string.IsNullOrWhiteSpace(_queueName))
        {
            throw new InvalidOperationException(
                $"'{ScaEventOptions.SectionName}:{nameof(ScaEventOptions.QueueName)}' must be configured.");
        }
    }

    public async Task PublishAsync<TRequest, TResponse>(
        ScaEventEnvelope<TRequest, TResponse> envelope,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await PublishCoreAsync(envelope, cancellationToken);
            _metrics.RecordEventPublished(envelope.Type);
        }
        catch (Exception ex)
        {
            _metrics.RecordEventPublishFailed(envelope.Type, ex);
            throw;
        }
    }

    private async Task PublishCoreAsync<TRequest, TResponse>(
        ScaEventEnvelope<TRequest, TResponse> envelope,
        CancellationToken cancellationToken)
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = envelope.EventId.ToString(),
            Type = envelope.Type,
            Timestamp = new AmqpTimestamp(envelope.OccurredAt.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                ["version"] = envelope.Version,
                ["idempotency-key"] = envelope.IdempotencyKey
            }
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _queueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
