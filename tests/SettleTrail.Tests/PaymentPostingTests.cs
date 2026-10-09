using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SettleTrail.Application.Accounts;
using SettleTrail.Application.Payments;
using SettleTrail.Infrastructure.Persistence;

namespace SettleTrail.Tests;

public sealed class PaymentPostingTests
{
    [Fact]
    public async Task A_transfer_posts_balanced_entries_and_updates_balances()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = await CreateDbAsync(connection);
        var posting = CreatePosting(db);
        var alice = await CreateAccountAsync(db, "Alice");
        var bob = await CreateAccountAsync(db, "Bob");

        await posting.PostAsync(PaymentPosting.TreasuryAccountId, alice.Id, 10_000, "fund-alice", "Deposit", default);
        var payment = await posting.PostAsync(alice.Id, bob.Id, 2_500, "transfer-1", "Transfer", default);

        var entries = await db.LedgerEntries.Where(e => e.PaymentId == payment.Id).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Equal(0, entries.Sum(e => e.AmountMinor));
        Assert.Equal(7_500, (await GetAccountHandler.Handle(new GetAccount(alice.Id), new AccountRepository(db), new LedgerRepository(db), default)).Account!.BalanceMinor);
        Assert.Equal(2_500, (await GetAccountHandler.Handle(new GetAccount(bob.Id), new AccountRepository(db), new LedgerRepository(db), default)).Account!.BalanceMinor);
    }

    [Fact]
    public async Task Repeating_a_key_returns_the_same_payment_without_new_entries()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = await CreateDbAsync(connection);
        var posting = CreatePosting(db);
        var account = await CreateAccountAsync(db, "Alice");

        var first = await posting.PostAsync(PaymentPosting.TreasuryAccountId, account.Id, 1_000, "deposit-1", "Deposit", default);
        var second = await posting.PostAsync(PaymentPosting.TreasuryAccountId, account.Id, 1_000, "deposit-1", "Deposit", default);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, await db.LedgerEntries.CountAsync());
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_amount_is_rejected()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = await CreateDbAsync(connection);
        var posting = CreatePosting(db);
        var account = await CreateAccountAsync(db, "Alice");
        await posting.PostAsync(PaymentPosting.TreasuryAccountId, account.Id, 1_000, "deposit-1", "Deposit", default);

        var error = await Assert.ThrowsAsync<PaymentException>(() =>
            posting.PostAsync(PaymentPosting.TreasuryAccountId, account.Id, 2_000, "deposit-1", "Deposit", default));
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public async Task A_transfer_with_insufficient_funds_creates_no_payment()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = await CreateDbAsync(connection);
        var posting = CreatePosting(db);
        var alice = await CreateAccountAsync(db, "Alice");
        var bob = await CreateAccountAsync(db, "Bob");

        var error = await Assert.ThrowsAsync<PaymentException>(() =>
            posting.PostAsync(alice.Id, bob.Id, 100, "transfer-1", "Transfer", default));
        Assert.Equal(409, error.StatusCode);
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Empty(await db.LedgerEntries.ToListAsync());
    }

    private static PaymentPosting CreatePosting(SettleTrailDbContext db) =>
        new(new AccountRepository(db), new PaymentRepository(db), new LedgerRepository(db), new EfUnitOfWork(db));

    private static Task<AccountResult> CreateAccountAsync(SettleTrailDbContext db, string name) =>
        CreateAccountHandler.Handle(new CreateAccount(name), new AccountRepository(db), new EfUnitOfWork(db), default);

    private static async Task<SettleTrailDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<SettleTrailDbContext>().UseSqlite(connection).Options;
        var db = new SettleTrailDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await db.SeedAsync();
        return db;
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }
}
