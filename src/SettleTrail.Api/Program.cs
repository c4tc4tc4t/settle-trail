using Microsoft.EntityFrameworkCore;
using SettleTrail.Api.Data;
using SettleTrail.Api.Payments;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SettleTrailDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SettleTrail")
        ?? "Data Source=settletrail.db"));
builder.Services.AddScoped<PaymentService>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SettleTrailDbContext>();
    await db.Database.MigrateAsync();
    await db.SeedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/accounts", async (CreateAccountRequest request, PaymentService service, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { error = "Account name is required." });

    var account = await service.CreateAccountAsync(request.Name.Trim(), cancellationToken);
    return Results.Created($"/accounts/{account.Id}", new AccountResponse(account.Id, account.Name, 0));
});

app.MapGet("/accounts/{id:guid}", async (Guid id, PaymentService service, CancellationToken cancellationToken) =>
{
    var account = await service.GetAccountAsync(id, cancellationToken);
    return account is null
        ? Results.NotFound()
        : Results.Ok(new AccountResponse(account.Id, account.Name, account.BalanceMinor));
});

app.MapPost("/accounts/{id:guid}/deposits", async (Guid id, MoneyRequest request, PaymentService service, CancellationToken cancellationToken) =>
{
    try
    {
        var payment = await service.DepositAsync(id, request.AmountMinor, request.IdempotencyKey, cancellationToken);
        return Results.Ok(new PaymentResponse(payment.Id, payment.SourceAccountId, payment.DestinationAccountId, payment.AmountMinor, payment.Kind, payment.CreatedAt));
    }
    catch (PaymentException exception)
    {
        return Results.Problem(exception.Message, statusCode: exception.StatusCode);
    }
});

app.MapPost("/payments", async (TransferRequest request, PaymentService service, CancellationToken cancellationToken) =>
{
    try
    {
        var payment = await service.TransferAsync(request.SourceAccountId, request.DestinationAccountId,
            request.AmountMinor, request.IdempotencyKey, cancellationToken);
        return Results.Ok(new PaymentResponse(payment.Id, payment.SourceAccountId, payment.DestinationAccountId, payment.AmountMinor, payment.Kind, payment.CreatedAt));
    }
    catch (PaymentException exception)
    {
        return Results.Problem(exception.Message, statusCode: exception.StatusCode);
    }
});

app.MapGet("/payments/{id:guid}", async (Guid id, SettleTrailDbContext db, CancellationToken cancellationToken) =>
{
    var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    return payment is null
        ? Results.NotFound()
        : Results.Ok(new PaymentResponse(payment.Id, payment.SourceAccountId, payment.DestinationAccountId, payment.AmountMinor, payment.Kind, payment.CreatedAt));
});

app.Run();

public partial class Program;

public record CreateAccountRequest(string Name);
public record MoneyRequest(long AmountMinor, string IdempotencyKey);
public record TransferRequest(Guid SourceAccountId, Guid DestinationAccountId, long AmountMinor, string IdempotencyKey);
public record AccountResponse(Guid Id, string Name, long BalanceMinor);
public record PaymentResponse(Guid Id, Guid SourceAccountId, Guid DestinationAccountId, long AmountMinor, string Kind, DateTimeOffset CreatedAt);
