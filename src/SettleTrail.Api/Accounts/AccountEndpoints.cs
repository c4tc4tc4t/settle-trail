using FluentValidation;
using SettleTrail.Api.Accounts.Contracts;
using SettleTrail.Api.Shared;
using SettleTrail.Application.Accounts;
using Wolverine;

namespace SettleTrail.Api.Accounts;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/accounts");

        accounts.MapPost("", async (CreateAccountRequest request, IValidator<CreateAccount> validator,
            IMessageBus bus, CancellationToken cancellationToken) =>
        {
            var command = new CreateAccount(request.Name);
            var error = await ApiProblems.ValidateAsync(command, validator, cancellationToken);
            if (error is not null) return error;
            var account = await bus.InvokeAsync<AccountResult>(command, cancellationToken);
            return Results.Created($"/accounts/{account.Id}", new AccountResponse(account.Id, account.Name, 0));
        })
            .Produces<AccountResponse>(StatusCodes.Status201Created)
            .Produces<ProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json");

        accounts.MapGet("/{id}", async (string id, IMessageBus bus, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(id, out var accountId) || accountId == Guid.Empty)
                return ApiProblems.InvalidRequest("id must be a non-empty GUID.");

            var account = (await bus.InvokeAsync<AccountQueryResult>(new GetAccount(accountId), cancellationToken)).Account;
            return account is null
                ? ApiProblems.NotFound("Account not found.")
                : Results.Ok(new AccountResponse(account.Id, account.Name, account.BalanceMinor));
        })
            .Produces<AccountResponse>()
            .Produces<ProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemResponse>(StatusCodes.Status404NotFound, "application/problem+json");
    }
}
