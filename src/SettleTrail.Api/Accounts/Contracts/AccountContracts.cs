using System.ComponentModel.DataAnnotations;

namespace SettleTrail.Api.Accounts.Contracts;

public sealed record CreateAccountRequest([property: Required, StringLength(100)] string? Name);
public sealed record AccountResponse(Guid Id, string Name, long BalanceMinor);
