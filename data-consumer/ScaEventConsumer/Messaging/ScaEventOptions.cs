namespace ScaEventConsumer.Messaging;

public sealed class ScaEventOptions
{
    public const string SectionName = "ScaEvents";

    public string QueueName { get; set; } = "sca-transaction-events";

    public ushort PrefetchCount { get; set; } = 20;
}
