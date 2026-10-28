using CustomerMockApi.Models;
using MongoDB.Driver;

namespace CustomerMockApi.Repositories;

public sealed class MongoCustomerTransactionRepository(IMongoDatabase database) : ICustomerTransactionRepository
{
    private const string CollectionName = "transactions";
    private readonly IMongoCollection<CustomerTransactionRecord> _collection =
        database.GetCollection<CustomerTransactionRecord>(CollectionName);

    public async Task<IReadOnlyList<CustomerTransactionRecord>> GetByAccountNumberAsync(
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(t => t.AccountNumber == accountNumber)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
