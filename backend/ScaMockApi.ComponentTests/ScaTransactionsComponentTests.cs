using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using ScaMockApi.Models;

namespace ScaMockApi.ComponentTests;

public class ScaTransactionsComponentTests : IDisposable
{
    private readonly HttpClient _client;

    public ScaTransactionsComponentTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseUrl = configuration["ScaMockApi:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = configuration["services:scamockapi:http:0"];
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "ScaMockApi URL was not configured. Ensure 'ScaMockApi__BaseUrl' is injected via .NET Aspire.");
        }

        _client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/healthcheck");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StartTransaction_AboveThreshold_ReturnsChallengeRequiredWithFactors()
    {
        var response = await _client.PostAsJsonAsync("/api/sca/transactions/start", new StartScaTransactionRequest(
            AccountNumber: "12345678",
            TransactionAmount: 50.00m
        ));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var transaction = await response.Content.ReadFromJsonAsync<ScaTransaction>();
        Assert.NotNull(transaction);
        Assert.NotEqual(Guid.Empty, transaction.TransactionId);
        Assert.Equal("12345678", transaction.AccountNumber);
        Assert.Equal(50.00m, transaction.TransactionAmount);
        Assert.Equal("CHALLENGE_REQUIRED", transaction.Status);
        Assert.Equal("SCA_STEP_UP_REQUIRED", transaction.ReasonCode);
        Assert.Equal(["otp", "email", "biometric"], transaction.Factors);
    }

    [Fact]
    public async Task StartTransaction_BelowThreshold_ReturnsExemptApproved()
    {
        var response = await _client.PostAsJsonAsync("/api/sca/transactions/start", new StartScaTransactionRequest(
            AccountNumber: "12345678",
            TransactionAmount: 15.00m
        ));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var transaction = await response.Content.ReadFromJsonAsync<ScaTransaction>();
        Assert.NotNull(transaction);
        Assert.Equal("EXEMPT_APPROVED", transaction.Status);
        Assert.Equal("LOW_VALUE_EXEMPTION", transaction.ReasonCode);
    }

    [Fact]
    public async Task IssueFactor_ValidFactor_ReturnsFactorIssuedResponse()
    {
        var startResponse = await _client.PostAsJsonAsync("/api/sca/transactions/start", new StartScaTransactionRequest(
            AccountNumber: "87654321",
            TransactionAmount: 100.00m
        ));
        var started = await startResponse.Content.ReadFromJsonAsync<ScaTransaction>();
        Assert.NotNull(started);

        var issueResponse = await _client.PostAsJsonAsync("/api/sca/transactions/issue", new IssueScaFactorRequest(
            TransactionId: started.TransactionId,
            AccountNumber: "87654321",
            SelectedFactor: "biometric"
        ));

        Assert.Equal(HttpStatusCode.OK, issueResponse.StatusCode);

        var updated = await issueResponse.Content.ReadFromJsonAsync<ScaTransaction>();
        Assert.NotNull(updated);
        Assert.Equal(started.TransactionId, updated.TransactionId);
        Assert.Equal("FACTOR_ISSUED", updated.Status);
        Assert.Equal("biometric", updated.SelectedFactor);
        Assert.Equal("FACTOR_BIOMETRIC_ISSUED", updated.ReasonCode);
        Assert.NotNull(updated.FactorIssuedAt);
    }

    [Fact]
    public async Task VerifyTransaction_AfterFactorIssued_ReturnsAuthenticatedResponse()
    {
        var startResponse = await _client.PostAsJsonAsync("/api/sca/transactions/start", new StartScaTransactionRequest(
            AccountNumber: "99990000",
            TransactionAmount: 75.00m
        ));
        var started = await startResponse.Content.ReadFromJsonAsync<ScaTransaction>();
        Assert.NotNull(started);

        await _client.PostAsJsonAsync("/api/sca/transactions/issue", new IssueScaFactorRequest(
            TransactionId: started.TransactionId,
            AccountNumber: "99990000",
            SelectedFactor: "otp"
        ));

        var verifyResponse = await _client.PostAsJsonAsync("/api/sca/transactions/verify", new VerifyScaTransactionRequest(
            TransactionId: started.TransactionId,
            AccountNumber: "99990000"
        ));

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var verification = await verifyResponse.Content.ReadFromJsonAsync<VerifyScaTransactionResponse>();
        Assert.NotNull(verification);
        Assert.Equal(started.TransactionId, verification.TransactionId);
        Assert.True(verification.IsAuthenticated);
        Assert.Equal("AUTHENTICATED", verification.Status);
        Assert.Equal("SCA_AUTH_SUCCESS", verification.ReasonCode);
        Assert.Equal("otp", verification.SelectedFactor);
    }

    [Fact]
    public async Task VerifyTransaction_WithoutFactorIssued_ReturnsAuthFailedResponse()
    {
        var startResponse = await _client.PostAsJsonAsync("/api/sca/transactions/start", new StartScaTransactionRequest(
            AccountNumber: "99990000",
            TransactionAmount: 75.00m
        ));
        var started = await startResponse.Content.ReadFromJsonAsync<ScaTransaction>();
        Assert.NotNull(started);

        var verifyResponse = await _client.PostAsJsonAsync("/api/sca/transactions/verify", new VerifyScaTransactionRequest(
            TransactionId: started.TransactionId,
            AccountNumber: "99990000"
        ));

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var verification = await verifyResponse.Content.ReadFromJsonAsync<VerifyScaTransactionResponse>();
        Assert.NotNull(verification);
        Assert.Equal(started.TransactionId, verification.TransactionId);
        Assert.False(verification.IsAuthenticated);
        Assert.Equal("AUTH_FAILED", verification.Status);
        Assert.Equal("SCA_FACTOR_NOT_ISSUED", verification.ReasonCode);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
