using ScaEventConsumer.Models;

namespace ScaEventConsumer.Services;

public interface IScaEventProcessor
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    Task ProcessAsync(RawScaEventEnvelope rawEvent, CancellationToken cancellationToken = default);
}
