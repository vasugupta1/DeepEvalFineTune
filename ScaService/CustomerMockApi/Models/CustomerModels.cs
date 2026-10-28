using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CustomerMockApi.Models;

public sealed record CustomerProfile(string AccountNumber, string Name, string Email);

public static class KnownCustomers
{
    public static readonly IReadOnlyList<CustomerProfile> All =
    [
        new("10000001", "Amelia Patel", "amelia.patel@example-bank.com"),
        new("10000002", "Oliver Chen", "oliver.chen@example-bank.com"),
        new("10000003", "Priya Walker", "priya.walker@example-bank.com"),
        new("10000004", "Liam Davies", "liam.davies@example-bank.com"),
        new("10000005", "Sofia Moreno", "sofia.moreno@example-bank.com"),
        new("10000006", "Noah Kowalski", "noah.kowalski@example-bank.com"),
        new("10000007", "Elena Okafor", "elena.okafor@example-bank.com"),
        new("10000008", "Marcus Lindqvist", "marcus.lindqvist@example-bank.com"),
        new("10000009", "Hannah Hughes", "hannah.hughes@example-bank.com"),
        new("10000010", "Lucas Reyes", "lucas.reyes@example-bank.com"),
        new("10000011", "Maya Campbell", "maya.campbell@example-bank.com"),
        new("10000012", "Ethan Fischer", "ethan.fischer@example-bank.com"),
        new("10000013", "Chloe Bennett", "chloe.bennett@example-bank.com"),
        new("10000014", "Daniel Nakamura", "daniel.nakamura@example-bank.com"),
        new("10000015", "Zara Silva", "zara.silva@example-bank.com"),
        new("10000016", "Aiden Murphy", "aiden.murphy@example-bank.com")
    ];

    public static readonly IReadOnlyDictionary<string, CustomerProfile> ByAccountNumber =
        All.ToDictionary(c => c.AccountNumber, StringComparer.Ordinal);
}

public sealed record CustomerLookupRequest(string AccountNumber);

public sealed record CustomerDataResponse(
    string AccountNumber,
    string Name,
    string Email,
    IReadOnlyList<CustomerTransactionRecord> TransactionHistory
);

public sealed record CustomerErrorResponse(string Error);

[BsonIgnoreExtraElements]
public sealed record CustomerTransactionRecord(
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
