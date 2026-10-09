using SettleTrail.Application.Abstractions;
using SettleTrail.Domain.Accounts;
using SettleTrail.Domain.Ledger;
using SettleTrail.Domain.Payments;

namespace SettleTrail.Application.Payments;

public sealed class PaymentPosting(IAccountRepository accounts, IPaymentRepository payments,
    ILedgerRepository ledger, IUnitOfWork unitOfWork)
{
    public static readonly Guid TreasuryAccountId = TreasuryAccount.Id;

    public async Task<PaymentResult> PostAsync(Guid sourceId, Guid destinationId, long amountMinor,
        string idempotencyKey, string kind, CancellationToken cancellationToken)
    {
        if (amountMinor <= 0)
            throw new PaymentException("AmountMinor must be greater than zero.", 400, "invalid_request");
        if (sourceId == destinationId)
            throw new PaymentException("Source and destination must differ.", 400, "invalid_request");
        if (!DepositValidator.ValidKey(idempotencyKey))
            throw new PaymentException("IdempotencyKey must have 1 to 100 characters.", 400, "invalid_request");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var existing = await payments.FindByKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.SourceAccountId != sourceId || existing.DestinationAccountId != destinationId
                || existing.AmountMinor != amountMinor || existing.Kind != kind)
                throw new PaymentException("IdempotencyKey was already used for a different payment.", 409, "idempotency_key_conflict");
            return ToResult(existing);
        }

        var source = await accounts.FindAsync(sourceId, cancellationToken);
        var destination = await accounts.FindAsync(destinationId, cancellationToken);
        if (source is null || destination is null || destination.IsSystem
            || (source.IsSystem && kind != "Deposit") || (!source.IsSystem && kind == "Deposit"))
            throw new PaymentException("Account not found.", 404, "resource_not_found");

        if (!source.IsSystem && await ledger.BalanceAsync(sourceId, cancellationToken) < amountMinor)
            throw new PaymentException("Insufficient funds.", 409, "insufficient_funds");

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
        payments.Add(payment);
        ledger.AddEntries(LedgerPosting.ForPayment(payment));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(payment);
    }

    public static PaymentResult ToResult(Payment payment) =>
        new(payment.Id, payment.SourceAccountId, payment.DestinationAccountId, payment.AmountMinor, payment.Kind, payment.CreatedAt);
}

public sealed class PaymentException(string message, int statusCode, string code) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
