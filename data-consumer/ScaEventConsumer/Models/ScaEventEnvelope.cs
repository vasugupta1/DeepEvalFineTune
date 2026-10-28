using System.Text.Json;

namespace ScaEventConsumer.Models;

/// <summary>
/// Envelope published by ScaMockApi for every SCA API interaction.
/// </summary>
public sealed record ScaEventEnvelope<TRequest, TResponse>(
    string Version,
    string IdempotencyKey,
    string Type,
    Guid EventId,
    DateTimeOffset OccurredAt,
    ScaEventRequest<TRequest> Request,
    ScaEventResponse<TResponse> Response
);

public sealed record ScaEventRequest<TBody>(string Method, string Path, TBody Body);

public sealed record ScaEventResponse<TBody>(int StatusCode, TBody Body);

public sealed record RawScaEventEnvelope(
    ScaEventEnvelope<JsonElement, JsonElement> Envelope,
    string RawJson
);
