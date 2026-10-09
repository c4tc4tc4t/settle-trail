using Microsoft.EntityFrameworkCore;

using SettleTrail.Domain.Accounts;
using SettleTrail.Domain.Ledger;
using SettleTrail.Domain.Payments;

namespace SettleTrail.Infrastructure.Persistence;

public sealed class SettleTrailDbContext(DbContextOptions<SettleTrailDbContext> options) : DbContext(options)
{
    public static readonly Guid TreasuryAccountId = TreasuryAccount.Id;

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>().HasKey(a => a.Id);
        modelBuilder.Entity<Payment>().HasKey(p => p.Id);
        modelBuilder.Entity<Payment>().HasIndex(p => p.IdempotencyKey).IsUnique();
        modelBuilder.Entity<LedgerEntry>().HasKey(e => e.Id);
        modelBuilder.Entity<LedgerEntry>().HasIndex(e => e.AccountId);
        modelBuilder.Entity<LedgerEntry>().HasIndex(e => e.PaymentId);
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await Accounts.AnyAsync(a => a.Id == TreasuryAccountId, cancellationToken))
            return;

        Accounts.Add(new Account
        {
            Id = TreasuryAccountId,
            Name = "Demo treasury",
            IsSystem = true
        });
        await SaveChangesAsync(cancellationToken);
    }
}

