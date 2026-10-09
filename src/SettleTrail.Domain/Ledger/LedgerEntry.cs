namespace SettleTrail.Domain.Ledger;

public sealed class LedgerEntry
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid AccountId { get; set; }
    public long AmountMinor { get; set; }
}
