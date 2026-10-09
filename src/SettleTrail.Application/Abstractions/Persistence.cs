using SettleTrail.Domain.Accounts;
using SettleTrail.Domain.Ledger;
using SettleTrail.Domain.Payments;

namespace SettleTrail.Application.Abstractions;

public interface IAccountRepository
{
    Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken);
    void Add(Account account);
}

public interface IPaymentRepository
{
    Task<Payment?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<Payment?> FindByKeyAsync(string key, CancellationToken cancellationToken);
    void Add(Payment payment);
}

public interface ILedgerRepository
{
    Task<long> BalanceAsync(Guid accountId, CancellationToken cancellationToken);
    void AddEntries(IEnumerable<LedgerEntry> entries);
}

public interface IUnitOfWork
{
    Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
