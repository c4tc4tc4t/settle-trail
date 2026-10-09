using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SettleTrail.Application.Abstractions;
using SettleTrail.Domain.Accounts;
using SettleTrail.Domain.Ledger;
using SettleTrail.Domain.Payments;

namespace SettleTrail.Infrastructure.Persistence;

public sealed class AccountRepository(SettleTrailDbContext db) : IAccountRepository
{
    public Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public void Add(Account account) => db.Accounts.Add(account);
}

public sealed class PaymentRepository(SettleTrailDbContext db) : IPaymentRepository
{
    public Task<Payment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Payment?> FindByKeyAsync(string key, CancellationToken cancellationToken) =>
        db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.IdempotencyKey == key, cancellationToken);

    public void Add(Payment payment) => db.Payments.Add(payment);
}

public sealed class LedgerRepository(SettleTrailDbContext db) : ILedgerRepository
{
    public async Task<long> BalanceAsync(Guid accountId, CancellationToken cancellationToken) =>
        await db.LedgerEntries.Where(e => e.AccountId == accountId)
            .SumAsync(e => (long?)e.AmountMinor, cancellationToken) ?? 0;

    public void AddEntries(IEnumerable<LedgerEntry> entries) => db.LedgerEntries.AddRange(entries);
}

public sealed class EfUnitOfWork(SettleTrailDbContext db) : IUnitOfWork
{
    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new EfTransaction(await db.Database.BeginTransactionAsync(cancellationToken));

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await db.SaveChangesAsync(cancellationToken);

    private sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
