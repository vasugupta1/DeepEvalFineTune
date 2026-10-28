using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using ScaMockApi.Models;
using Xunit.Gherkin.Quick;

namespace ScaMockApi.ComponentTests;

[FeatureFile("./Features/ScaTransactions.feature")]
public sealed class ScaTransactionsComponentTests : Feature, IDisposable
{
    private readonly HttpClient _client;
    private string _accountNumber = string.Empty;
    private decimal _transactionAmount;
    private HttpResponseMessage? _lastResponse;
    private ScaTransaction? _currentTransaction;
    private VerifyScaTransactionResponse? _lastVerificationResponse;

    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>The Idempotency-Key sent with the most recent request.</summary>
    public string? LastIdempotencyKey { get; private set; }

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

    /// <summary>
    /// Posts JSON with a unique <c>Idempotency-Key</c> header, which ScaMockApi stamps onto the
    /// published <c>ScaEventEnvelope</c>.
    /// </summary>
    private async Task<HttpResponseMessage> PostWithIdempotencyKeyAsync<TRequest>(string path, TRequest body)
    {
        LastIdempotencyKey = $"comp-test-{Guid.NewGuid()}";

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add(IdempotencyKeyHeader, LastIdempotencyKey);

        _lastResponse?.Dispose();
        return await _client.SendAsync(request);
    }

    [Given(@"an account number ""(.*)"" and transaction amount (.*)")]
    public void GivenAnAccountNumberAndTransactionAmount(string accountNumber, string amount)
    {
        _accountNumber = accountNumber;
        _transactionAmount = decimal.Parse(amount, CultureInfo.InvariantCulture);
    }

    [When(@"I call the healthcheck endpoint")]
    public async Task WhenICallTheHealthcheckEndpoint()
    {
        _lastResponse = await _client.GetAsync("/api/healthcheck");
    }

    [When(@"I start an SCA transaction")]
    public async Task WhenIStartAnScaTransaction()
    {
        _lastResponse = await PostWithIdempotencyKeyAsync(
            "/api/sca/transactions/start",
            new StartScaTransactionRequest(_accountNumber, _transactionAmount));

        if (_lastResponse.IsSuccessStatusCode)
        {
            _currentTransaction = await _lastResponse.Content.ReadFromJsonAsync<ScaTransaction>();
        }
    }

    [When(@"I issue the factor ""(.*)"" for account ""(.*)""")]
    [And(@"I issue the factor ""(.*)"" for account ""(.*)""")]
    public async Task WhenIIssueTheFactorForAccount(string factor, string accountNumber)
    {
        Assert.NotNull(_currentTransaction);

        _lastResponse = await PostWithIdempotencyKeyAsync(
            "/api/sca/transactions/issue",
            new IssueScaFactorRequest(_currentTransaction.TransactionId, accountNumber, factor));

        if (_lastResponse.IsSuccessStatusCode)
        {
            _currentTransaction = await _lastResponse.Content.ReadFromJsonAsync<ScaTransaction>();
        }
    }

    [When(@"I verify the transaction for account ""(.*)""")]
    [And(@"I verify the transaction for account ""(.*)""")]
    public async Task WhenIVerifyTheTransactionForAccount(string accountNumber)
    {
        Assert.NotNull(_currentTransaction);

        _lastResponse = await PostWithIdempotencyKeyAsync(
            "/api/sca/transactions/verify",
            new VerifyScaTransactionRequest(_currentTransaction.TransactionId, accountNumber));

        if (_lastResponse.IsSuccessStatusCode)
        {
            _lastVerificationResponse = await _lastResponse.Content.ReadFromJsonAsync<VerifyScaTransactionResponse>();
        }
    }

    [When(@"I verify an unknown transaction ID for account ""(.*)""")]
    public async Task WhenIVerifyAnUnknownTransactionIdForAccount(string accountNumber)
    {
        _lastResponse = await PostWithIdempotencyKeyAsync(
            "/api/sca/transactions/verify",
            new VerifyScaTransactionRequest(Guid.NewGuid(), accountNumber));
    }

    [Then(@"the response status code should be (\d+)")]
    public void ThenTheResponseStatusCodeShouldBe(int expectedStatusCode)
    {
        Assert.NotNull(_lastResponse);
        Assert.Equal(expectedStatusCode, (int)_lastResponse.StatusCode);
    }

    [Then(@"the transaction status should be ""(.*)""")]
    [And(@"the transaction status should be ""(.*)""")]
    public void ThenTheTransactionStatusShouldBe(string expectedStatus)
    {
        Assert.NotNull(_currentTransaction);
        Assert.Equal(expectedStatus, _currentTransaction.Status);
    }

    [Then(@"the transaction reason code should be ""(.*)""")]
    [And(@"the transaction reason code should be ""(.*)""")]
    public void ThenTheTransactionReasonCodeShouldBe(string expectedReasonCode)
    {
        Assert.NotNull(_currentTransaction);
        Assert.Equal(expectedReasonCode, _currentTransaction.ReasonCode);
    }

    [Then(@"the available factors should include ""(.*)""")]
    [And(@"the available factors should include ""(.*)""")]
    public void ThenTheAvailableFactorsShouldInclude(string expectedFactorsCsv)
    {
        Assert.NotNull(_currentTransaction);
        var expectedFactors = expectedFactorsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(expectedFactors, _currentTransaction.Factors);
    }

    [Then(@"the selected factor should be ""(.*)""")]
    [And(@"the selected factor should be ""(.*)""")]
    public void ThenTheSelectedFactorShouldBe(string expectedFactor)
    {
        Assert.NotNull(_currentTransaction);
        Assert.Equal(expectedFactor, _currentTransaction.SelectedFactor);
    }

    [Then(@"the verification result should be authenticated (true|false)")]
    [And(@"the verification result should be authenticated (true|false)")]
    public void ThenTheVerificationResultShouldBeAuthenticated(string expectedAuthenticated)
    {
        Assert.NotNull(_lastVerificationResponse);
        Assert.Equal(bool.Parse(expectedAuthenticated), _lastVerificationResponse.IsAuthenticated);
    }

    [Then(@"the verification status should be ""(.*)""")]
    [And(@"the verification status should be ""(.*)""")]
    public void ThenTheVerificationStatusShouldBe(string expectedStatus)
    {
        Assert.NotNull(_lastVerificationResponse);
        Assert.Equal(expectedStatus, _lastVerificationResponse.Status);
    }

    [Then(@"the verification reason code should be ""(.*)""")]
    [And(@"the verification reason code should be ""(.*)""")]
    public void ThenTheVerificationReasonCodeShouldBe(string expectedReasonCode)
    {
        Assert.NotNull(_lastVerificationResponse);
        Assert.Equal(expectedReasonCode, _lastVerificationResponse.ReasonCode);
    }

    [Then(@"the verified selected factor should be ""(.*)""")]
    [And(@"the verified selected factor should be ""(.*)""")]
    public void ThenTheVerifiedSelectedFactorShouldBe(string expectedFactor)
    {
        Assert.NotNull(_lastVerificationResponse);
        Assert.Equal(expectedFactor, _lastVerificationResponse.SelectedFactor);
    }

    public void Dispose()
    {
        _lastResponse?.Dispose();
        _client.Dispose();
    }
}
