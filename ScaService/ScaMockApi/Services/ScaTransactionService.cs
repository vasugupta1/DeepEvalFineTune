using ScaMockApi.Messaging;
using ScaMockApi.Models;
using ScaMockApi.Repositories;

namespace ScaMockApi.Services;

/// <summary>
/// SCA transaction business logic. Every outcome (success or failure) publishes a
/// <see cref="ScaEventEnvelope{TRequest,TResponse}"/> that mirrors the result returned to the caller.
/// </summary>
public class ScaTransactionService : IScaTransactionService
{
    private readonly IScaTransactionRepository _repository;
    private readonly IScaEventPublisher _eventPublisher;

    public ScaTransactionService(IScaTransactionRepository repository, IScaEventPublisher eventPublisher)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
    }

    public async Task<ScaServiceResult> StartAsync(
        StartScaTransactionRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var events = new EventContext<StartScaTransactionRequest>(
            this, ScaRoutes.StartTransaction, request, idempotencyKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.AccountNumber))
        {
            return await events.PublishAsync(ScaEventTypes.TransactionStartFailed, StatusCodes.Status400BadRequest,
                new ScaErrorResponse("AccountNumber is required."));
        }

        if (request.TransactionAmount <= 0)
        {
            return await events.PublishAsync(ScaEventTypes.TransactionStartFailed, StatusCodes.Status400BadRequest,
                new ScaErrorResponse("TransactionAmount must be greater than zero."));
        }

        var isLowValueExempt = request.TransactionAmount < 30m;
        var status = isLowValueExempt ? "EXEMPT_APPROVED" : "CHALLENGE_REQUIRED";
        var reasonCode = isLowValueExempt ? "LOW_VALUE_EXEMPTION" : "SCA_STEP_UP_REQUIRED";

        var transaction = new ScaTransaction(
            TransactionId: Guid.NewGuid(),
            AccountNumber: request.AccountNumber,
            TransactionAmount: request.TransactionAmount,
            Status: status,
            ReasonCode: reasonCode,
            Factors: ScaFactors.All,
            CreatedAt: DateTimeOffset.UtcNow
        );

        await _repository.CreateAsync(transaction, cancellationToken);

        return await events.PublishAsync(ScaEventTypes.TransactionStarted, StatusCodes.Status200OK, transaction);
    }

    public async Task<ScaServiceResult> IssueFactorAsync(
        IssueScaFactorRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var events = new EventContext<IssueScaFactorRequest>(
            this, ScaRoutes.IssueFactor, request, idempotencyKey, cancellationToken);

        if (request.TransactionId == Guid.Empty)
        {
            return await events.PublishAsync(ScaEventTypes.FactorIssueFailed, StatusCodes.Status400BadRequest,
                new ScaErrorResponse("TransactionId is required."));
        }

        if (string.IsNullOrWhiteSpace(request.AccountNumber))
        {
            return await events.PublishAsync(ScaEventTypes.FactorIssueFailed, StatusCodes.Status400BadRequest,
                new ScaErrorResponse("AccountNumber is required."));
        }

        if (string.IsNullOrWhiteSpace(request.SelectedFactor))
        {
            return await events.PublishAsync(ScaEventTypes.FactorIssueFailed, StatusCodes.Status400BadRequest,
                new ScaErrorResponse("SelectedFactor is required."));
        }

        var transaction = await _repository.GetByIdAsync(request.TransactionId, cancellationToken);
        if (transaction is null)
        {
            return await events.PublishAsync(ScaEventTypes.FactorIssueFailed, StatusCodes.Status404NotFound,
                new ScaErrorResponse($"Transaction '{request.TransactionId}' was not found."));
        }

        if (!string.Equals(transaction.AccountNumber, request.AccountNumber, StringComparison.Ordinal))
        {
            return await events.PublishAsync(ScaEventTypes.FactorIssueFailed, StatusCodes.Status400BadRequest,
                new ScaErrorResponse("AccountNumber does not match the transaction."));
        }

        var normalizedFactor = request.SelectedFactor.Trim().ToLowerInvariant();
        if (!transaction.Factors.Contains(normalizedFactor, StringComparer.OrdinalIgnoreCase))
        {
            return await events.PublishAsync(ScaEventTypes.FactorIssueFailed, StatusCodes.Status400BadRequest,
                new ScaErrorResponse(
                    $"Factor '{request.SelectedFactor}' is not valid for this transaction.",
                    transaction.Factors
                ));
        }

        var updatedTransaction = transaction with
        {
            SelectedFactor = normalizedFactor,
            Status = "FACTOR_ISSUED",
            ReasonCode = $"FACTOR_{normalizedFactor.ToUpperInvariant()}_ISSUED",
            FactorIssuedAt = DateTimeOffset.UtcNow
        };

        await _repository.UpdateAsync(updatedTransaction, cancellationToken);

        return await events.PublishAsync(ScaEventTypes.FactorIssued, StatusCodes.Status200OK, updatedTransaction);
    }

    public async Task<ScaServiceResult> VerifyAsync(
        VerifyScaTransactionRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var events = new EventContext<VerifyScaTransactionRequest>(
            this, ScaRoutes.VerifyTransaction, request, idempotencyKey, cancellationToken);

        if (request.TransactionId == Guid.Empty)
        {
            return await events.PublishAsync(ScaEventTypes.TransactionVerifyFailed, StatusCodes.Status400BadRequest,
                new ScaErrorResponse("TransactionId is required."));
        }

        if (string.IsNullOrWhiteSpace(request.AccountNumber))
        {
            return await events.PublishAsync(ScaEventTypes.TransactionVerifyFailed, StatusCodes.Status400BadRequest,
                new ScaErrorResponse("AccountNumber is required."));
        }

        var transaction = await _repository.GetByIdAsync(request.TransactionId, cancellationToken);
        if (transaction is null)
        {
            return await events.PublishAsync(ScaEventTypes.TransactionVerifyFailed, StatusCodes.Status404NotFound,
                new ScaErrorResponse($"Transaction '{request.TransactionId}' was not found."));
        }

        var verifiedAt = DateTimeOffset.UtcNow;

        if (!string.Equals(transaction.AccountNumber, request.AccountNumber, StringComparison.Ordinal))
        {
            var mismatchResponse = new VerifyScaTransactionResponse(
                TransactionId: transaction.TransactionId,
                AccountNumber: request.AccountNumber,
                IsAuthenticated: false,
                Status: "AUTH_FAILED",
                ReasonCode: "ACCOUNT_NUMBER_MISMATCH",
                SelectedFactor: transaction.SelectedFactor,
                VerifiedAt: verifiedAt
            );
            return await events.PublishAsync(ScaEventTypes.TransactionVerified, StatusCodes.Status200OK, mismatchResponse);
        }

        var isSuccess = transaction.Status is "EXEMPT_APPROVED" or "FACTOR_ISSUED" or "AUTHENTICATED";
        var status = isSuccess ? "AUTHENTICATED" : "AUTH_FAILED";
        var reasonCode = isSuccess
            ? (transaction.Status == "EXEMPT_APPROVED" ? "LOW_VALUE_EXEMPTION_VERIFIED" : "SCA_AUTH_SUCCESS")
            : "SCA_FACTOR_NOT_ISSUED";

        var updatedTransaction = transaction with
        {
            Status = status,
            ReasonCode = reasonCode,
            IsAuthenticated = isSuccess,
            VerifiedAt = verifiedAt
        };

        await _repository.UpdateAsync(updatedTransaction, cancellationToken);

        var response = new VerifyScaTransactionResponse(
            TransactionId: updatedTransaction.TransactionId,
            AccountNumber: updatedTransaction.AccountNumber,
            IsAuthenticated: isSuccess,
            Status: updatedTransaction.Status,
            ReasonCode: updatedTransaction.ReasonCode,
            SelectedFactor: updatedTransaction.SelectedFactor,
            VerifiedAt: verifiedAt
        );

        return await events.PublishAsync(ScaEventTypes.TransactionVerified, StatusCodes.Status200OK, response);
    }

    /// <summary>
    /// Captures the per-call request details so each outcome only has to supply the event type,
    /// status code and response body.
    /// </summary>
    private sealed class EventContext<TRequest>
    {
        private readonly ScaTransactionService _service;
        private readonly string _path;
        private readonly TRequest _request;
        private readonly string _idempotencyKey;
        private readonly CancellationToken _cancellationToken;

        public EventContext(
            ScaTransactionService service,
            string path,
            TRequest request,
            string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            _service = service;
            _path = path;
            _request = request;
            // A key is generated when the caller does not supply an Idempotency-Key header.
            _idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey.Trim();
            _cancellationToken = cancellationToken;
        }

        public async Task<ScaServiceResult> PublishAsync<TResponse>(string eventType, int statusCode, TResponse response)
            where TResponse : notnull
        {
            var envelope = ScaEventEnvelope.Create(
                type: eventType,
                idempotencyKey: _idempotencyKey,
                method: HttpMethods.Post,
                path: _path,
                requestBody: _request,
                statusCode: statusCode,
                responseBody: response);

            await _service._eventPublisher.PublishAsync(envelope, _cancellationToken);

            return new ScaServiceResult(statusCode, response);
        }
    }
}
