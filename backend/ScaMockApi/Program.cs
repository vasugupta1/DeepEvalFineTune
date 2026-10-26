using ScaMockApi.Messaging;
using ScaMockApi.Models;
using ScaMockApi.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.AddMongoDBClient("scatransactions");
builder.AddRabbitMQClient("deepeval-rabbitmq");

builder.Services.AddScoped<IScaTransactionRepository, MongoScaTransactionRepository>();
builder.Services.AddScoped<IScaEventPublisher, RabbitMqScaEventPublisher>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/api/healthcheck");

app.MapPost("/api/sca/transactions/start", async (
    StartScaTransactionRequest request,
    IScaTransactionRepository repository,
    IScaEventPublisher eventPublisher,
    CancellationToken cancellationToken) =>
{
    const string endpoint = "/api/sca/transactions/start";

    if (string.IsNullOrWhiteSpace(request.AccountNumber))
    {
        var error = new ScaErrorResponse("AccountNumber is required.");
        await eventPublisher.PublishResponseAsync("ScaTransactionStartFailed", endpoint, StatusCodes.Status400BadRequest, error, cancellationToken);
        return Results.BadRequest(error);
    }

    if (request.TransactionAmount <= 0)
    {
        var error = new ScaErrorResponse("TransactionAmount must be greater than zero.");
        await eventPublisher.PublishResponseAsync("ScaTransactionStartFailed", endpoint, StatusCodes.Status400BadRequest, error, cancellationToken);
        return Results.BadRequest(error);
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

    await repository.CreateAsync(transaction, cancellationToken);
    await eventPublisher.PublishResponseAsync("ScaTransactionStarted", endpoint, StatusCodes.Status200OK, transaction, cancellationToken);

    return Results.Ok(transaction);
})
.WithName("StartScaTransaction");

app.MapPost("/api/sca/transactions/issue", async (
    IssueScaFactorRequest request,
    IScaTransactionRepository repository,
    IScaEventPublisher eventPublisher,
    CancellationToken cancellationToken) =>
{
    const string endpoint = "/api/sca/transactions/issue";

    if (request.TransactionId == Guid.Empty)
    {
        var error = new ScaErrorResponse("TransactionId is required.");
        await eventPublisher.PublishResponseAsync("ScaFactorIssueFailed", endpoint, StatusCodes.Status400BadRequest, error, cancellationToken);
        return Results.BadRequest(error);
    }

    if (string.IsNullOrWhiteSpace(request.AccountNumber))
    {
        var error = new ScaErrorResponse("AccountNumber is required.");
        await eventPublisher.PublishResponseAsync("ScaFactorIssueFailed", endpoint, StatusCodes.Status400BadRequest, error, cancellationToken);
        return Results.BadRequest(error);
    }

    if (string.IsNullOrWhiteSpace(request.SelectedFactor))
    {
        var error = new ScaErrorResponse("SelectedFactor is required.");
        await eventPublisher.PublishResponseAsync("ScaFactorIssueFailed", endpoint, StatusCodes.Status400BadRequest, error, cancellationToken);
        return Results.BadRequest(error);
    }

    var transaction = await repository.GetByIdAsync(request.TransactionId, cancellationToken);
    if (transaction is null)
    {
        var error = new ScaErrorResponse($"Transaction '{request.TransactionId}' was not found.");
        await eventPublisher.PublishResponseAsync("ScaFactorIssueFailed", endpoint, StatusCodes.Status404NotFound, error, cancellationToken);
        return Results.NotFound(error);
    }

    if (!string.Equals(transaction.AccountNumber, request.AccountNumber, StringComparison.Ordinal))
    {
        var error = new ScaErrorResponse("AccountNumber does not match the transaction.");
        await eventPublisher.PublishResponseAsync("ScaFactorIssueFailed", endpoint, StatusCodes.Status400BadRequest, error, cancellationToken);
        return Results.BadRequest(error);
    }

    var normalizedFactor = request.SelectedFactor.Trim().ToLowerInvariant();
    if (!transaction.Factors.Contains(normalizedFactor, StringComparer.OrdinalIgnoreCase))
    {
        var error = new ScaErrorResponse(
            $"Factor '{request.SelectedFactor}' is not valid for this transaction.",
            transaction.Factors
        );
        await eventPublisher.PublishResponseAsync("ScaFactorIssueFailed", endpoint, StatusCodes.Status400BadRequest, error, cancellationToken);
        return Results.BadRequest(error);
    }

    var updatedTransaction = transaction with
    {
        SelectedFactor = normalizedFactor,
        Status = "FACTOR_ISSUED",
        ReasonCode = $"FACTOR_{normalizedFactor.ToUpperInvariant()}_ISSUED",
        FactorIssuedAt = DateTimeOffset.UtcNow
    };

    await repository.UpdateAsync(updatedTransaction, cancellationToken);
    await eventPublisher.PublishResponseAsync("ScaFactorIssued", endpoint, StatusCodes.Status200OK, updatedTransaction, cancellationToken);

    return Results.Ok(updatedTransaction);
})
.WithName("IssueScaFactor");

app.MapPost("/api/sca/transactions/verify", async (
    VerifyScaTransactionRequest request,
    IScaTransactionRepository repository,
    IScaEventPublisher eventPublisher,
    CancellationToken cancellationToken) =>
{
    const string endpoint = "/api/sca/transactions/verify";

    if (request.TransactionId == Guid.Empty)
    {
        var error = new ScaErrorResponse("TransactionId is required.");
        await eventPublisher.PublishResponseAsync("ScaTransactionVerifyFailed", endpoint, StatusCodes.Status400BadRequest, error, cancellationToken);
        return Results.BadRequest(error);
    }

    if (string.IsNullOrWhiteSpace(request.AccountNumber))
    {
        var error = new ScaErrorResponse("AccountNumber is required.");
        await eventPublisher.PublishResponseAsync("ScaTransactionVerifyFailed", endpoint, StatusCodes.Status400BadRequest, error, cancellationToken);
        return Results.BadRequest(error);
    }

    var transaction = await repository.GetByIdAsync(request.TransactionId, cancellationToken);
    if (transaction is null)
    {
        var error = new ScaErrorResponse($"Transaction '{request.TransactionId}' was not found.");
        await eventPublisher.PublishResponseAsync("ScaTransactionVerifyFailed", endpoint, StatusCodes.Status404NotFound, error, cancellationToken);
        return Results.NotFound(error);
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
        await eventPublisher.PublishResponseAsync("ScaTransactionVerified", endpoint, StatusCodes.Status200OK, mismatchResponse, cancellationToken);
        return Results.Ok(mismatchResponse);
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

    await repository.UpdateAsync(updatedTransaction, cancellationToken);

    var response = new VerifyScaTransactionResponse(
        TransactionId: updatedTransaction.TransactionId,
        AccountNumber: updatedTransaction.AccountNumber,
        IsAuthenticated: isSuccess,
        Status: updatedTransaction.Status,
        ReasonCode: updatedTransaction.ReasonCode,
        SelectedFactor: updatedTransaction.SelectedFactor,
        VerifiedAt: verifiedAt
    );

    await eventPublisher.PublishResponseAsync("ScaTransactionVerified", endpoint, StatusCodes.Status200OK, response, cancellationToken);

    return Results.Ok(response);
})
.WithName("VerifyScaTransaction");

app.Run();
