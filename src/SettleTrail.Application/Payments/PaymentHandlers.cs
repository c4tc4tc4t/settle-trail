using SettleTrail.Application.Abstractions;
using SettleTrail.Domain.Payments;

namespace SettleTrail.Application.Payments;

public static class DepositHandler
{
    public static Task<PaymentResult> Handle(Deposit command, PaymentPosting posting, CancellationToken cancellationToken) =>
        posting.PostAsync(PaymentPosting.TreasuryAccountId, command.DestinationAccountId,
            command.AmountMinor!.Value, command.IdempotencyKey!, "Deposit", cancellationToken);
}

public static class TransferHandler
{
    public static Task<PaymentResult> Handle(Transfer command, PaymentPosting posting, CancellationToken cancellationToken) =>
        posting.PostAsync(command.SourceAccountId!.Value, command.DestinationAccountId!.Value,
            command.AmountMinor!.Value, command.IdempotencyKey!, "Transfer", cancellationToken);
}

public static class GetPaymentHandler
{
    public static async Task<PaymentQueryResult> Handle(GetPayment query, IPaymentRepository payments,
        CancellationToken cancellationToken)
    {
        var payment = await payments.FindAsync(query.Id, cancellationToken);
        return new PaymentQueryResult(payment is null ? null : PaymentPosting.ToResult(payment));
    }
}
