using SettleTrail.Application.Abstractions;
using SettleTrail.Domain.Accounts;

namespace SettleTrail.Application.Accounts;

public static class CreateAccountHandler
{
    public static async Task<AccountResult> Handle(CreateAccount command, IAccountRepository accounts,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var account = new Account { Id = Guid.NewGuid(), Name = command.Name!.Trim() };
        accounts.Add(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AccountResult(account.Id, account.Name, 0);
    }
}

public static class GetAccountHandler
{
    public static async Task<AccountQueryResult> Handle(GetAccount query, IAccountRepository accounts,
        ILedgerRepository ledger, CancellationToken cancellationToken)
    {
        var account = await accounts.FindAsync(query.Id, cancellationToken);
        if (account is null || account.IsSystem) return new AccountQueryResult(null);
        var balance = await ledger.BalanceAsync(account.Id, cancellationToken);
        return new AccountQueryResult(new AccountResult(account.Id, account.Name, balance));
    }
}
