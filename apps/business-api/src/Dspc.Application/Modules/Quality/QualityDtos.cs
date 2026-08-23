using Dspc.Application.Common;
using FluentValidation;

namespace Dspc.Application.Modules.Quality;

public sealed record InspectionDto(Guid Id, string Result, string? Notes, DateTime InspectedAt, string? Inspector, string? Code);

public sealed record NonConformanceDto(Guid Id, string Code, string Title, string Status, DateTime RaisedAt, string? Description, string? LotNumber);

public sealed record LotSummaryDto(
    string LotNumber, string? HeatNumber, string PartCode, string? PartName, string SupplierCode, string? SupplierName,
    decimal Quantity, decimal Remaining, string Unit, string Status, DateOnly? ReceivedOn, string? Country);

public sealed record LotConsumptionDto(string OrderCode, IReadOnlyList<string> Serials, decimal Quantity);

public sealed record LotDto(
    string LotNumber, string? HeatNumber, string PartCode, string? PartName, string SupplierCode, string? SupplierName,
    decimal Quantity, decimal Remaining, string Unit, string Status, DateOnly? ReceivedOn, string? Country,
    Guid? PoLineId, string? PoCode, DateOnly? ProducedOn, DateOnly? ExpiresOn, string? BlockReason, DateTime? BlockedAt,
    IReadOnlyList<DocumentSummary> Documents, IReadOnlyList<InspectionDto> Inspections,
    IReadOnlyList<LotConsumptionDto> ConsumedBy, IReadOnlyList<string> ReservedBy,
    IReadOnlyList<NonConformanceDto> NonConformances, string RowVersion);

public sealed record BlockLotRequest(string Reason, string NcrTitle);

public sealed record AffectedRecordsDto(IReadOnlyList<string> Orders, IReadOnlyList<string> Serials, IReadOnlyList<string> Passports);

public sealed record BlockLotResponse(LotDto Lot, AffectedRecordsDto Affected, NonConformanceDto NonConformance);

public sealed record AddInspectionRequest(string Result, string? Notes, DateTime? InspectedAt);

public sealed class BlockLotRequestValidator : AbstractValidator<BlockLotRequest>
{
    public BlockLotRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.NcrTitle).NotEmpty().MaximumLength(200);
    }
}

public sealed class AddInspectionRequestValidator : AbstractValidator<AddInspectionRequest>
{
    public AddInspectionRequestValidator()
    {
        RuleFor(x => x.Result).Must(r => r is "Passed" or "Failed" or "Conditional").WithMessage("Result must be Passed, Failed or Conditional.");
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
