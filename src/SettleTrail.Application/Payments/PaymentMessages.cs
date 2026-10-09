namespace SettleTrail.Application.Payments;

public sealed record Deposit(Guid DestinationAccountId, long? AmountMinor, string? IdempotencyKey);
public sealed record Transfer(Guid? SourceAccountId, Guid? DestinationAccountId, long? AmountMinor, string? IdempotencyKey);
public sealed record GetPayment(Guid Id);
public sealed record PaymentResult(Guid Id, Guid SourceAccountId, Guid DestinationAccountId, long AmountMinor, string Kind, DateTimeOffset CreatedAt);
public sealed record PaymentQueryResult(PaymentResult? Payment);
