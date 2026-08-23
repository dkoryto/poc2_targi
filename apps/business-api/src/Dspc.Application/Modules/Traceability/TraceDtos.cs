namespace Dspc.Application.Modules.Traceability;

public sealed record TraceSearchHit(string Kind, string Code, string Label);

public sealed record TraceNode(string Kind, string Code, string Label, string? Status, IReadOnlyList<TraceNode> Children, IReadOnlyDictionary<string, object?>? Meta = null);

public sealed record TraceComponentDto(
    string PartCode, string? PartName, string LotNumber, string? HeatNumber, string SupplierCode, string? SupplierName,
    string? Country, string? CertSha256, Guid? DocumentId, string LotStatus, decimal Quantity);

public sealed record SerialTraceDto(
    string Serial, string ProductCode, string ProductName, string OrderCode, string BomVersion, string Status,
    TraceNode Genealogy, IReadOnlyList<TraceComponentDto> Components, string? PassportStatus,
    // Reachable by deep link and by scanning a passport QR, so the record names its own plant.
    string SiteCode = "", string SiteName = "");

public sealed record LotForwardOrderDto(string OrderCode, string Status, string Relation);
public sealed record LotForwardSerialDto(string Serial, string OrderCode, string ProductCode);
public sealed record LotForwardPassportDto(string Serial, string Status);

public sealed record LotForwardDto(
    Modules.Quality.LotSummaryDto Lot,
    IReadOnlyList<LotForwardOrderDto> Orders,
    IReadOnlyList<LotForwardSerialDto> Serials,
    IReadOnlyList<LotForwardPassportDto> Passports);
