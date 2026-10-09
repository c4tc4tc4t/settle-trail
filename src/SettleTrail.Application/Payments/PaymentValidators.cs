using FluentValidation;

namespace SettleTrail.Application.Payments;

public sealed class DepositValidator : AbstractValidator<Deposit>
{
    public DepositValidator()
    {
        RuleFor(x => x.DestinationAccountId).NotEmpty().WithMessage("id must be a non-empty GUID.");
        RuleFor(x => x.AmountMinor).NotNull().GreaterThan(0).WithMessage("amountMinor must be greater than zero.");
        RuleFor(x => x.IdempotencyKey).Must(ValidKey).WithMessage("idempotencyKey must contain 1 to 100 non-control characters.");
    }

    internal static bool ValidKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && key.Length <= 100 && !key.Any(char.IsControl);
}

public sealed class TransferValidator : AbstractValidator<Transfer>
{
    public TransferValidator()
    {
        RuleFor(x => x.SourceAccountId).Must(id => id.HasValue && id.Value != Guid.Empty)
            .WithMessage("sourceAccountId must be a non-empty GUID.");
        RuleFor(x => x.DestinationAccountId).Must(id => id.HasValue && id.Value != Guid.Empty)
            .WithMessage("destinationAccountId must be a non-empty GUID.");
        RuleFor(x => x).Must(x => x.SourceAccountId != x.DestinationAccountId)
            .When(x => x.SourceAccountId is not null && x.DestinationAccountId is not null)
            .WithMessage("sourceAccountId and destinationAccountId must differ.");
        RuleFor(x => x.AmountMinor).NotNull().GreaterThan(0).WithMessage("amountMinor must be greater than zero.");
        RuleFor(x => x.IdempotencyKey).Must(DepositValidator.ValidKey).WithMessage("idempotencyKey must contain 1 to 100 non-control characters.");
    }
}
