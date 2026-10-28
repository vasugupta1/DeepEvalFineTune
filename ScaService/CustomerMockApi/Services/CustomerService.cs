using System.Diagnostics;
using CustomerMockApi.Models;
using CustomerMockApi.Repositories;
using CustomerMockApi.Telemetry;

namespace CustomerMockApi.Services;

public sealed class CustomerService(
    ICustomerTransactionRepository repository,
    CustomerMetrics metrics) : ICustomerService
{
    public async Task<CustomerServiceResult> GetCustomerByAccountNumberAsync(
        string? accountNumber,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();

        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            var error = new CustomerErrorResponse("AccountNumber is required.");
            metrics.RecordRequestProcessed(endpoint, StatusCodes.Status400BadRequest, Stopwatch.GetElapsedTime(startedAt));
            return new CustomerServiceResult(StatusCodes.Status400BadRequest, error);
        }

        var normalizedAccount = accountNumber.Trim();
        if (!KnownCustomers.ByAccountNumber.TryGetValue(normalizedAccount, out var customer))
        {
            var notFound = new CustomerErrorResponse($"Customer with account number '{normalizedAccount}' was not found.");
            metrics.RecordRequestProcessed(endpoint, StatusCodes.Status404NotFound, Stopwatch.GetElapsedTime(startedAt));
            return new CustomerServiceResult(StatusCodes.Status404NotFound, notFound);
        }

        var transactions = await repository.GetByAccountNumberAsync(normalizedAccount, cancellationToken);

        var response = new CustomerDataResponse(
            AccountNumber: customer.AccountNumber,
            Name: customer.Name,
            Email: customer.Email,
            TransactionHistory: transactions);

        metrics.RecordRequestProcessed(endpoint, StatusCodes.Status200OK, Stopwatch.GetElapsedTime(startedAt));
        return new CustomerServiceResult(StatusCodes.Status200OK, response);
    }
}
