using FluentValidation;

namespace SettleTrail.Application.Accounts;

public sealed class CreateAccountValidator : AbstractValidator<CreateAccount>
{
    public CreateAccountValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 100 && !name.Any(char.IsControl))
            .WithMessage("name must contain 1 to 100 non-control characters.");
    }
}
