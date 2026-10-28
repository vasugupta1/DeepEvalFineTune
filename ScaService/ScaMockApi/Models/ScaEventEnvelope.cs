namespace ScaMockApi.Models;

/// <summary>
/// Single envelope published for every SCA API interaction.
/// Root properties describe the event; <see cref="Request"/> and <see cref="Response"/>
/// carry the generic HTTP request/response payloads.
/// </summary>
public record ScaEventEnvelope<TRequest, TResponse>(
    string Version,
    string IdempotencyKey,
    string Type,
    Guid EventId,
    DateTimeOffset OccurredAt,
    ScaEventRequest<TRequest> Request,
    ScaEventResponse<TResponse> Response
);

public record ScaEventRequest<TBody>(string Method, string Path, TBody Body);

public record ScaEventResponse<TBody>(int StatusCode, TBody Body);

public static class ScaEventEnvelope
{
    public const string CurrentVersion = "1.0";

    public static ScaEventEnvelope<TRequest, TResponse> Create<TRequest, TResponse>(
        string type,
        string idempotencyKey,
        string method,
        string path,
        TRequest requestBody,
        int statusCode,
        TResponse responseBody) =>
        new(
            Version: CurrentVersion,
            IdempotencyKey: idempotencyKey,
            Type: type,
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            Request: new ScaEventRequest<TRequest>(method, path, requestBody),
            Response: new ScaEventResponse<TResponse>(statusCode, responseBody)
        );
}

public static class ScaEventTypes
{
    public const string TransactionStarted = "ScaTransactionStarted";
    public const string TransactionStartFailed = "ScaTransactionStartFailed";
    public const string FactorIssued = "ScaFactorIssued";
    public const string FactorIssueFailed = "ScaFactorIssueFailed";
    public const string TransactionVerified = "ScaTransactionVerified";
    public const string TransactionVerifyFailed = "ScaTransactionVerifyFailed";
}
