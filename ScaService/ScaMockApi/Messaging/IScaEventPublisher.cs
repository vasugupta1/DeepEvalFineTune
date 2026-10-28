using ScaMockApi.Models;

namespace ScaMockApi.Messaging;

public class ScaEventOptions
{
    public const string SectionName = "ScaEvents";

    public string QueueName { get; set; } = string.Empty;
}

public interface IScaEventPublisher
{
    Task PublishAsync<TRequest, TResponse>(
        ScaEventEnvelope<TRequest, TResponse> envelope,
        CancellationToken cancellationToken = default);
}
