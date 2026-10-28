using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ScaEventConsumer.Models;
using ScaEventConsumer.Services;
using ScaEventConsumer.Telemetry;

namespace ScaEventConsumer.Messaging;

public sealed class ScaEventConsumerWorker(
    IConnection connection,
    IScaEventProcessor processor,
    ScaConsumerMetrics metrics,
    IOptions<ScaEventOptions> options,
    ILogger<ScaEventConsumerWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueName = options.Value.QueueName;
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new InvalidOperationException(
                $"'{ScaEventOptions.SectionName}:{nameof(ScaEventOptions.QueueName)}' must be configured.");
        }

        await processor.EnsureSchemaAsync(stoppingToken);

        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: options.Value.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var startedAt = Stopwatch.GetTimestamp();
            string? eventType = ea.BasicProperties.Type;

            try
            {
                var rawJson = Encoding.UTF8.GetString(ea.Body.Span);
                var envelope = JsonSerializer.Deserialize<ScaEventEnvelope<JsonElement, JsonElement>>(
                    rawJson,
                    SerializerOptions)
                    ?? throw new InvalidOperationException("Deserialized ScaEventEnvelope was null.");

                eventType = envelope.Type;

                await processor.ProcessAsync(new RawScaEventEnvelope(envelope, rawJson), ea.CancellationToken);

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ea.CancellationToken);

                metrics.RecordProcessed(
                    queueName,
                    envelope.Type,
                    envelope.Response.StatusCode,
                    Stopwatch.GetElapsedTime(startedAt));

                logger.LogInformation(
                    "Processed SCA event {EventId} ({EventType}, IdempotencyKey={IdempotencyKey}, Status={StatusCode})",
                    envelope.EventId,
                    envelope.Type,
                    envelope.IdempotencyKey,
                    envelope.Response.StatusCode);
            }
            catch (Exception ex)
            {
                metrics.RecordFailed(queueName, eventType, ex, Stopwatch.GetElapsedTime(startedAt));
                logger.LogError(ex, "Failed processing message {DeliveryTag} from {Queue}", ea.DeliveryTag, queueName);

                await _channel.BasicNackAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    ea.CancellationToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("Listening for SCA events on RabbitMQ queue '{QueueName}'.", queueName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
