namespace SettleTrail.Domain.Payments;

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
