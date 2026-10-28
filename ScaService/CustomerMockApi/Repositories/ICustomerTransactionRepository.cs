using CustomerMockApi.Models;

namespace CustomerMockApi.Repositories;

public interface ICustomerTransactionRepository
{
    Task<IReadOnlyList<CustomerTransactionRecord>> GetByAccountNumberAsync(
        string accountNumber,
        CancellationToken cancellationToken = default);
}
