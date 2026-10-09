using FluentValidation;
using SettleTrail.Api.Payments.Contracts;
using SettleTrail.Api.Shared;
using SettleTrail.Application.Payments;
using Wolverine;

namespace SettleTrail.Api.Payments;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/accounts/{id}/deposits", async (string id, MoneyRequest request, IValidator<Deposit> validator,
            IMessageBus bus, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(id, out var accountId) || accountId == Guid.Empty)
                return ApiProblems.InvalidRequest("id must be a non-empty GUID.");
            var command = new Deposit(accountId, request.AmountMinor, request.IdempotencyKey);
            var error = await ApiProblems.ValidateAsync(command, validator, cancellationToken);
            if (error is not null) return error;
            var payment = await bus.InvokeAsync<PaymentResult>(command, cancellationToken);
            return Results.Ok(PaymentResponse.From(payment));
        })
            .Produces<PaymentResponse>()
            .Produces<ProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemResponse>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<ProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json");

        var payments = app.MapGroup("/payments");

        payments.MapPost("", async (TransferRequest request, IValidator<Transfer> validator,
            IMessageBus bus, CancellationToken cancellationToken) =>
        {
            var command = new Transfer(request.SourceAccountId, request.DestinationAccountId,
                request.AmountMinor, request.IdempotencyKey);
            var error = await ApiProblems.ValidateAsync(command, validator, cancellationToken);
            if (error is not null) return error;
            var payment = await bus.InvokeAsync<PaymentResult>(command, cancellationToken);
            return Results.Ok(PaymentResponse.From(payment));
        })
            .Produces<PaymentResponse>()
            .Produces<ProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemResponse>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<ProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json");

        payments.MapGet("/{id}", async (string id, IMessageBus bus, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(id, out var paymentId) || paymentId == Guid.Empty)
                return ApiProblems.InvalidRequest("id must be a non-empty GUID.");

            var payment = (await bus.InvokeAsync<PaymentQueryResult>(new GetPayment(paymentId), cancellationToken)).Payment;
            return payment is null
                ? ApiProblems.NotFound("Payment not found.")
                : Results.Ok(PaymentResponse.From(payment));
        })
            .Produces<PaymentResponse>()
            .Produces<ProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemResponse>(StatusCodes.Status404NotFound, "application/problem+json");
    }
}
