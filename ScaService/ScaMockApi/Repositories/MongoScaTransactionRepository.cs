using MongoDB.Driver;
using ScaMockApi.Models;

namespace ScaMockApi.Repositories;

public class MongoScaTransactionRepository : IScaTransactionRepository
{
    private const string CollectionName = "transactions";
    private readonly IMongoCollection<ScaTransaction> _collection;

    public MongoScaTransactionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ScaTransaction>(CollectionName);
    }

    public async Task<ScaTransaction> CreateAsync(ScaTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(transaction, cancellationToken: cancellationToken);
        return transaction;
    }

    public async Task<ScaTransaction?> GetByIdAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(t => t.TransactionId == transactionId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScaTransaction>> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(t => t.AccountNumber == accountNumber)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ScaTransaction?> GetLatestByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(t => t.AccountNumber == accountNumber)
            .SortByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(ScaTransaction transaction, CancellationToken cancellationToken = default)
    {
        var result = await _collection.ReplaceOneAsync(
            t => t.TransactionId == transaction.TransactionId,
            transaction,
            cancellationToken: cancellationToken);

        return result.IsAcknowledged && result.ModifiedCount > 0;
    }
}
