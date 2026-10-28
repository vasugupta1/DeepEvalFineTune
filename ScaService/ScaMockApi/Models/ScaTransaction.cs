using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ScaMockApi.Models;

public static class ScaFactors
{
    public const string Otp = "otp";
    public const string Email = "email";
    public const string Biometric = "biometric";

    public static readonly IReadOnlyList<string> All = [Otp, Email, Biometric];
}

public record StartScaTransactionRequest(string AccountNumber, decimal TransactionAmount);

public record IssueScaFactorRequest(Guid TransactionId, string AccountNumber, string SelectedFactor);

public record VerifyScaTransactionRequest(Guid TransactionId, string AccountNumber);

public record VerifyScaTransactionResponse(
    Guid TransactionId,
    string AccountNumber,
    bool IsAuthenticated,
    string Status,
    string ReasonCode,
    string? SelectedFactor,
    DateTimeOffset VerifiedAt
);

public record ScaErrorResponse(string Error, IReadOnlyList<string>? AllowedFactors = null);

public record ScaResponseEvent<TPayload>(
    Guid EventId,
    string EventType,
    string Endpoint,
    int StatusCode,
    TPayload Response,
    DateTimeOffset EmittedAt
);

public record ScaTransaction(
    [property: BsonId]
    [property: BsonRepresentation(BsonType.String)]
    Guid TransactionId,
    string AccountNumber,
    decimal TransactionAmount,
    string Status,
    string ReasonCode,
    IReadOnlyList<string> Factors,
    DateTimeOffset CreatedAt,
    string? SelectedFactor = null,
    DateTimeOffset? FactorIssuedAt = null,
    bool? IsAuthenticated = null,
    DateTimeOffset? VerifiedAt = null
);
