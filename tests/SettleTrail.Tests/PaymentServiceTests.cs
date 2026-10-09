using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SettleTrail.Api.Data;
using SettleTrail.Api.Payments;

namespace SettleTrail.Tests;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task A_transfer_posts_balanced_entries_and_updates_balances()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        await db.SeedAsync();
        var service = new PaymentService(db);

        var alice = await service.CreateAccountAsync("Alice");
        var bob = await service.CreateAccountAsync("Bob");
        await service.DepositAsync(alice.Id, 10_000, "fund-alice");
        var payment = await service.TransferAsync(alice.Id, bob.Id, 2_500, "transfer-1");

        var entries = await db.LedgerEntries.Where(e => e.PaymentId == payment.Id).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Equal(0, entries.Sum(e => e.AmountMinor));
        Assert.Equal(7_500, (await service.GetAccountAsync(alice.Id))!.BalanceMinor);
        Assert.Equal(2_500, (await service.GetAccountAsync(bob.Id))!.BalanceMinor);
    }

    [Fact]
    public async Task Repeating_a_key_returns_the_same_payment_without_new_entries()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        await db.SeedAsync();
        var service = new PaymentService(db);

        var account = await service.CreateAccountAsync("Alice");
        var first = await service.DepositAsync(account.Id, 1_000, "deposit-1");
        var second = await service.DepositAsync(account.Id, 1_000, "deposit-1");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, await db.LedgerEntries.CountAsync());
        Assert.Equal(1_000, (await service.GetAccountAsync(account.Id))!.BalanceMinor);
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_amount_is_rejected()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        await db.SeedAsync();
        var service = new PaymentService(db);

        var account = await service.CreateAccountAsync("Alice");
        await service.DepositAsync(account.Id, 1_000, "deposit-1");

        var error = await Assert.ThrowsAsync<PaymentException>(
            () => service.DepositAsync(account.Id, 2_000, "deposit-1"));
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public async Task A_transfer_with_insufficient_funds_creates_no_payment()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        await db.SeedAsync();
        var service = new PaymentService(db);

        var alice = await service.CreateAccountAsync("Alice");
        var bob = await service.CreateAccountAsync("Bob");

        var error = await Assert.ThrowsAsync<PaymentException>(
            () => service.TransferAsync(alice.Id, bob.Id, 100, "transfer-1"));
        Assert.Equal(409, error.StatusCode);
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Empty(await db.LedgerEntries.ToListAsync());
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static SettleTrailDbContext CreateDb(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<SettleTrailDbContext>()
            .UseSqlite(connection)
            .Options;
        return new SettleTrailDbContext(options);
    }
}
