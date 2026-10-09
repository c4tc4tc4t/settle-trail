namespace SettleTrail.Application.Accounts;

public sealed record CreateAccount(string? Name);
public sealed record GetAccount(Guid Id);
public sealed record AccountResult(Guid Id, string Name, long BalanceMinor);
public sealed record AccountQueryResult(AccountResult? Account);
