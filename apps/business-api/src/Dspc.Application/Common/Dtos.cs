namespace Dspc.Application.Common;

public sealed record ListResult<T>(IReadOnlyList<T> Items, int Total);

public static class ListResult
{
    public static ListResult<T> Of<T>(IReadOnlyList<T> items) => new(items, items.Count);
    public static ListResult<T> Of<T>(IReadOnlyList<T> items, int total) => new(items, total);
}

/// <summary>History row shape consumed by the web app (`ChangeEntry` in apps/web/src/api/types.ts).</summary>
public sealed record ChangeEntry(Guid Id, DateTime OccurredAt, string User, string Action, string? Field, string? Before, string? After, string? Comment);

public sealed record DocumentSummary(
    Guid Id, string Type, string DocumentNumber, string FileName, long SizeBytes, string Sha256, string Status,
    DateTime UploadedAt, string UploadedBy, string? LotNumber, string? HeatNumber, DateOnly? IssuedOn,
    string? VerifiedBy, DateTime? VerifiedAt, string? VerificationComment, object? AiSuggestion);

public sealed record RiskFactorDto(string Code, double Raw, double Weight, double Contribution);
public sealed record EndangeredOrderDto(string OrderCode, string ProductCode, int Priority, DateOnly RequiredOn, decimal Shortage, DateOnly? AvailableOn, int LatenessDays);
public sealed record RiskSummaryDto(int Score, string Category, IReadOnlyList<RiskFactorDto> Factors, IReadOnlyList<RiskFactorDto> TopFactors, IReadOnlyList<EndangeredOrderDto> EndangeredOrders, DateTime AssessedAt, string Method = "RuleBased");
