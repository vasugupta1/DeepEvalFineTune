namespace CustomerMockApi.Services;

public static class CustomerRoutes
{
    public const string ListCustomers = "/api/customers";
    public const string GetByAccountNumber = "/api/customers/{accountNumber}";
    public const string Lookup = "/api/customers/lookup";
}

public sealed record CustomerServiceResult(int StatusCode, object Body);

public interface ICustomerService
{
    Task<CustomerServiceResult> GetCustomerByAccountNumberAsync(
        string? accountNumber,
        string endpoint,
        CancellationToken cancellationToken = default);
}
