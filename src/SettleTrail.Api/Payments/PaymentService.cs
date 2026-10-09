using Microsoft.EntityFrameworkCore;
using SettleTrail.Api.Data;

namespace SettleTrail.Api.Payments;

public sealed class PaymentService(SettleTrailDbContext db)
{
    public async Task<Account> CreateAccountAsync(string name, CancellationToken cancellationToken = default)
    {
        var account = new Account { Id = Guid.NewGuid(), Name = name };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task<AccountWithBalance?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsSystem, cancellationToken);
        if (account is null)
            return null;

        var balance = await db.LedgerEntries.Where(e => e.AccountId == id)
            .SumAsync(e => (long?)e.AmountMinor, cancellationToken) ?? 0;
        return new AccountWithBalance(account.Id, account.Name, balance);
    }

    public Task<Payment> DepositAsync(Guid destinationAccountId, long amountMinor,
        string idempotencyKey, CancellationToken cancellationToken = default) =>
        PostAsync(SettleTrailDbContext.TreasuryAccountId, destinationAccountId,
            amountMinor, idempotencyKey, "Deposit", cancellationToken);

    public Task<Payment> TransferAsync(Guid sourceAccountId, Guid destinationAccountId,
        long amountMinor, string idempotencyKey, CancellationToken cancellationToken = default) =>
        PostAsync(sourceAccountId, destinationAccountId,
            amountMinor, idempotencyKey, "Transfer", cancellationToken);

    private async Task<Payment> PostAsync(Guid sourceId, Guid destinationId, long amountMinor,
        string idempotencyKey, string kind, CancellationToken cancellationToken)
    {
        if (amountMinor <= 0)
            throw new PaymentException("AmountMinor must be greater than zero.", 400);
        if (sourceId == destinationId)
            throw new PaymentException("Source and destination must differ.", 400);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
            throw new PaymentException("IdempotencyKey must have 1 to 100 characters.", 400);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.SourceAccountId != sourceId || existing.DestinationAccountId != destinationId
                || existing.AmountMinor != amountMinor || existing.Kind != kind)
                throw new PaymentException("IdempotencyKey was already used for a different payment.", 409);
            return existing;
        }

        var source = await db.Accounts.FirstOrDefaultAsync(a => a.Id == sourceId, cancellationToken);
        var destination = await db.Accounts.FirstOrDefaultAsync(a => a.Id == destinationId && !a.IsSystem, cancellationToken);
        if (source is null || destination is null || (source.IsSystem && kind != "Deposit")
            || (!source.IsSystem && kind == "Deposit"))
            throw new PaymentException("Account not found.", 404);

        if (!source.IsSystem)
        {
            var balance = await db.LedgerEntries.Where(e => e.AccountId == sourceId)
                .SumAsync(e => (long?)e.AmountMinor, cancellationToken) ?? 0;
            if (balance < amountMinor)
                throw new PaymentException("Insufficient funds.", 409);
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            SourceAccountId = sourceId,
            DestinationAccountId = destinationId,
            AmountMinor = amountMinor,
            IdempotencyKey = idempotencyKey,
            Kind = kind,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Payments.Add(payment);
        db.LedgerEntries.AddRange(
            new LedgerEntry { Id = Guid.NewGuid(), PaymentId = payment.Id, AccountId = sourceId, AmountMinor = -amountMinor },
            new LedgerEntry { Id = Guid.NewGuid(), PaymentId = payment.Id, AccountId = destinationId, AmountMinor = amountMinor });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return payment;
    }
}

public sealed record AccountWithBalance(Guid Id, string Name, long BalanceMinor);

public sealed class PaymentException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
