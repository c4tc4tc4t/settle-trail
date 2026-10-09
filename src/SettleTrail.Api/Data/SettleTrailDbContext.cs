using Microsoft.EntityFrameworkCore;

namespace SettleTrail.Api.Data;

public sealed class SettleTrailDbContext(DbContextOptions<SettleTrailDbContext> options) : DbContext(options)
{
    public static readonly Guid TreasuryAccountId = Guid.Parse("00000000-0000-0000-0000-000000000001");

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

public sealed class Account
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public bool IsSystem { get; set; }
}

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid SourceAccountId { get; set; }
    public Guid DestinationAccountId { get; set; }
    public long AmountMinor { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string Kind { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class LedgerEntry
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid AccountId { get; set; }
    public long AmountMinor { get; set; }
}
