using FluentValidation;
using SettleTrail.Application.Payments;

namespace SettleTrail.Api.Shared;

public static class ApiProblems
{
    public static async Task<IResult?> ValidateAsync<T>(T request, IValidator<T> validator, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        return validation.IsValid ? null : InvalidRequest(validation.Errors[0].ErrorMessage);
    }

    public static IResult InvalidRequest(string detail) => Create(400, "invalid_request", detail);
    public static IResult NotFound(string detail) => Create(404, "resource_not_found", detail);

    public static IResult FromPaymentException(PaymentException exception) =>
        Create(exception.StatusCode, exception.Code, exception.Message);

    private static IResult Create(int status, string code, string detail) =>
        Results.Problem(statusCode: status, title: status switch
        {
            400 => "Invalid request",
            404 => "Resource not found",
            409 => "Conflict",
            _ => "Request failed"
        }, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
}
