using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SettleTrail.Api.Accounts;
using SettleTrail.Api.Payments;
using SettleTrail.Api.Shared;
using SettleTrail.Application.Abstractions;
using SettleTrail.Application.Accounts;
using SettleTrail.Application.Payments;
using SettleTrail.Infrastructure.Persistence;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddDbContext<SettleTrailDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SettleTrail")
        ?? "Data Source=settletrail.db"));
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<ILedgerRepository, LedgerRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<PaymentPosting>();
builder.Services.AddScoped<IValidator<CreateAccount>, CreateAccountValidator>();
builder.Services.AddScoped<IValidator<Deposit>, DepositValidator>();
builder.Services.AddScoped<IValidator<Transfer>, TransferValidator>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Host.UseWolverine(options =>
{
    options.Discovery.IncludeAssembly(typeof(CreateAccountHandler).Assembly);
    options.CodeGeneration.AlwaysUseServiceLocationFor<SettleTrailDbContext>();
});

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var result = exception switch
    {
        BadHttpRequestException => ApiProblems.InvalidRequest("The request body or path is invalid."),
        ValidationException validation => ApiProblems.InvalidRequest(validation.Errors.FirstOrDefault()?.ErrorMessage ?? "The request is invalid."),
        PaymentException payment => ApiProblems.FromPaymentException(payment),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
    await result.ExecuteAsync(context);
}));

app.UseStatusCodePages(async context =>
{
    if (context.HttpContext.Response.StatusCode == StatusCodes.Status400BadRequest)
        await ApiProblems.InvalidRequest("The request body or path is invalid.").ExecuteAsync(context.HttpContext);
});

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SettleTrailDbContext>();
    await db.Database.MigrateAsync();
    await db.SeedAsync();
}

app.MapGet("/health", () => Results.Ok(new HealthResponse("healthy")))
    .Produces<HealthResponse>();
app.MapAccountEndpoints();
app.MapPaymentEndpoints();

app.Run();

public partial class Program;
