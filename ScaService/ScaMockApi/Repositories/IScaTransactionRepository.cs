using ScaMockApi.Models;

namespace ScaMockApi.Repositories;

public interface IScaTransactionRepository
{
    Task<ScaTransaction> CreateAsync(ScaTransaction transaction, CancellationToken cancellationToken = default);
    Task<ScaTransaction?> GetByIdAsync(Guid transactionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScaTransaction>> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
    Task<ScaTransaction?> GetLatestByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(ScaTransaction transaction, CancellationToken cancellationToken = default);
}
