namespace SettleTrail.Api.Shared;

public sealed record HealthResponse(string Status);
public sealed record ProblemResponse(string Type, string Title, int Status, string Detail, string Code);
