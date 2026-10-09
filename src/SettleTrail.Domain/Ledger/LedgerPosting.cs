using SettleTrail.Domain.Payments;

namespace SettleTrail.Domain.Ledger;

public static class LedgerPosting
{
    public static LedgerEntry[] ForPayment(Payment payment) =>
    [
        new() { Id = Guid.NewGuid(), PaymentId = payment.Id, AccountId = payment.SourceAccountId, AmountMinor = -payment.AmountMinor },
        new() { Id = Guid.NewGuid(), PaymentId = payment.Id, AccountId = payment.DestinationAccountId, AmountMinor = payment.AmountMinor }
    ];
}
