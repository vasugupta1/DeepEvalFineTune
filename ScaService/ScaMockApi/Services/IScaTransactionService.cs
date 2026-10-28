using ScaMockApi.Models;

namespace ScaMockApi.Services;

public static class ScaRoutes
{
    public const string StartTransaction = "/api/sca/transactions/start";
    public const string IssueFactor = "/api/sca/transactions/issue";
    public const string VerifyTransaction = "/api/sca/transactions/verify";
}

/// <summary>Outcome of an SCA operation: the HTTP status code and body to return to the caller.</summary>
public sealed record ScaServiceResult(int StatusCode, object Body);

public interface IScaTransactionService
{
    Task<ScaServiceResult> StartAsync(
        StartScaTransactionRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ScaServiceResult> IssueFactorAsync(
        IssueScaFactorRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ScaServiceResult> VerifyAsync(
        VerifyScaTransactionRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}
