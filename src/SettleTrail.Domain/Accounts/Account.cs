namespace SettleTrail.Domain.Accounts;

public sealed class Account
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public bool IsSystem { get; set; }
}
