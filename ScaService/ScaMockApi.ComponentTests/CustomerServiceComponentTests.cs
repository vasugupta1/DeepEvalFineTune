using System.Net.Http.Json;
using CustomerMockApi.Models;
using Microsoft.Extensions.Configuration;
using Xunit.Gherkin.Quick;

namespace ScaMockApi.ComponentTests;

[FeatureFile("./Features/CustomerService.feature")]
public sealed class CustomerServiceComponentTests : Feature, IDisposable
{
    private readonly HttpClient _customerClient;
    private HttpResponseMessage? _lastCustomerResponse;
    private CustomerDataResponse? _customerData;

    public CustomerServiceComponentTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var customerBaseUrl = configuration["CustomerMockApi:BaseUrl"]
            ?? configuration["services:customermockapi:http:0"];

        if (string.IsNullOrWhiteSpace(customerBaseUrl))
        {
            throw new InvalidOperationException(
                "CustomerMockApi URL was not configured. Ensure 'CustomerMockApi__BaseUrl' is injected via .NET Aspire.");
        }

        _customerClient = new HttpClient { BaseAddress = new Uri(customerBaseUrl) };
    }

    [When(@"I look up the customer for account ""(.*)""")]
    [And(@"I look up the customer for account ""(.*)""")]
    public async Task WhenILookUpTheCustomerForAccount(string accountNumber)
    {
        _lastCustomerResponse?.Dispose();
        _lastCustomerResponse = await _customerClient.GetAsync($"/api/customers/{accountNumber}");

        if (_lastCustomerResponse.IsSuccessStatusCode)
        {
            _customerData = await _lastCustomerResponse.Content.ReadFromJsonAsync<CustomerDataResponse>();
        }
    }

    [Then(@"the customer response status code should be (\d+)")]
    public void ThenTheCustomerResponseStatusCodeShouldBe(int expectedStatusCode)
    {
        Assert.NotNull(_lastCustomerResponse);
        Assert.Equal(expectedStatusCode, (int)_lastCustomerResponse.StatusCode);
    }

    [And(@"the customer name should be ""(.*)""")]
    public void ThenTheCustomerNameShouldBe(string expectedName)
    {
        Assert.NotNull(_customerData);
        Assert.Equal(expectedName, _customerData.Name);
    }

    [And(@"the customer email should be ""(.*)""")]
    public void ThenTheCustomerEmailShouldBe(string expectedEmail)
    {
        Assert.NotNull(_customerData);
        Assert.Equal(expectedEmail, _customerData.Email);
    }

    public void Dispose()
    {
        _lastCustomerResponse?.Dispose();
        _customerClient.Dispose();
    }
}
