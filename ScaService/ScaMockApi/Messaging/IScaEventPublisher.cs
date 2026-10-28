namespace ScaMockApi.Messaging;

public class ScaEventQueueOptions
{
    public const string SectionName = "ScaEventQueues";

    public string StartTransaction { get; set; } = string.Empty;
    public string IssueFactor { get; set; } = string.Empty;
    public string VerifyTransaction { get; set; } = string.Empty;
}

public interface IScaEventPublisher
{
    Task PublishResponseAsync<TPayload>(
        string queueName,
        string eventType,
        string endpoint,
        int statusCode,
        TPayload response,
        CancellationToken cancellationToken = default);
}
