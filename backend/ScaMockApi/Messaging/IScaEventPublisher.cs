namespace ScaMockApi.Messaging;

public interface IScaEventPublisher
{
    Task PublishResponseAsync<TPayload>(
        string eventType,
        string endpoint,
        int statusCode,
        TPayload response,
        CancellationToken cancellationToken = default);
}
