using System.ComponentModel.DataAnnotations;
using SettleTrail.Application.Payments;

namespace SettleTrail.Api.Payments.Contracts;

public sealed record MoneyRequest([property: Required, Range(1, long.MaxValue)] long? AmountMinor,
    [property: Required, StringLength(100)] string? IdempotencyKey);
public sealed record TransferRequest([property: Required] Guid? SourceAccountId,
    [property: Required] Guid? DestinationAccountId,
    [property: Required, Range(1, long.MaxValue)] long? AmountMinor,
    [property: Required, StringLength(100)] string? IdempotencyKey);
public sealed record PaymentResponse(Guid Id, Guid SourceAccountId, Guid DestinationAccountId, long AmountMinor, string Kind, DateTimeOffset CreatedAt)
{
    public static PaymentResponse From(PaymentResult payment) =>
        new(payment.Id, payment.SourceAccountId, payment.DestinationAccountId, payment.AmountMinor, payment.Kind, payment.CreatedAt);
}
