using Dspc.Application.Common;

namespace Dspc.Application.Modules.Inbound;

// Field names follow apps/web/src/api/types.ts (PurchaseOrderSummary): orderedAt, requiredDate (min), eta (max), riskScore (max), progressPercent (avg).
public sealed record PurchaseOrderSummaryDto(string Code, string SupplierCode, string SupplierName, string Status, DateOnly OrderedAt, DateOnly? RequiredDate, DateOnly? Eta, int LineCount, int OpenLines, int RiskScore, string RiskCategory, int ProgressPercent, string SiteCode);

public sealed record PurchaseOrderLineDto(
    Guid Id, int LineNo, string PartCode, string PartName, string PartNameEn, string Category, decimal Quantity, decimal DeliveredQuantity, string Unit,
    DateOnly RequiredDate, DateOnly Eta, DateOnly OriginalEta, int ProgressPercent, string Status, bool SupplierConfirmed,
    string? LotNumber, string? HeatNumber, DateOnly? ProducedOn, DateOnly? ExpiresOn, DateOnly? DeliveredOn,
    RiskSummaryDto Risk, IReadOnlyList<DocumentSummary> Documents, IReadOnlyList<string> RequiredDocumentTypes, string? ShipmentCode, string RowVersion, string? LastComment);

public sealed record PurchaseOrderDetailDto(string Code, SupplierRefDto Supplier, string Status, DateOnly OrderedAt, string? Notes, string SiteCode, IReadOnlyList<PurchaseOrderLineDto> Lines, IReadOnlyList<ChangeEntry> History, string RowVersion);
public sealed record SupplierRefDto(string Code, string Name, string Country, string City, double Lat, double Lon, double Otif, double QualityScore, int RiskScore, int OpenOrders, int ActiveShipments);

public sealed record PatchLineRequest(string? Status, int? ProgressPercent, string? LotNumber, string? HeatNumber, DateOnly? ProducedOn, DateOnly? ExpiresOn, decimal? Quantity, DateOnly? Eta, string? Comment);
public sealed record EtaChangeRequest(DateOnly Eta, string Reason, string? Comment);
public sealed record EtaChangeResponse(PurchaseOrderLineDto Line, RiskSummaryDto Risk, IReadOnlyList<EndangeredOrderDto> EndangeredOrders, double PredictedDowntimeHours);
public sealed record LineImpactDto(RiskSummaryDto Risk, IReadOnlyList<EndangeredOrderDto> EndangeredOrders, int EndangeredCount, double PredictedDowntimeHours, bool Restricted);

public sealed record ShipmentDto(string Code, string PoCode, string SupplierCode, string SupplierName, string Status, string? Carrier, string? Vehicle, DateTime? PlannedDeparture, DateTime? ActualDeparture, DateOnly Eta, DateOnly? RequiredDate, DateTime? ArrivedAt, double Progress, IReadOnlyList<ShipmentLineDto> Lines, IReadOnlyList<ShipmentEventDto> Events, int RiskScore, string RiskCategory, string RowVersion);
public sealed record ShipmentLineDto(Guid LineId, int LineNo, string PartCode, string PartName, decimal Quantity, string Unit, DateOnly RequiredDate);
public sealed record ShipmentEventDto(Guid Id, string Type, DateTime OccurredAt, string? Note, string? Location, string User);
public sealed record CreateShipmentRequest(string PoCode, IReadOnlyList<Guid> LineIds, string Carrier, string? Vehicle, DateTime PlannedDeparture, DateOnly Eta);
public sealed record AddShipmentEventRequest(string Type, DateTime? OccurredAt, string? Note, string? Location, double? Progress);

public sealed record LogisticsEventDto(Guid Id, string Code, string Type, string Severity, string? SupplierCode, string? ShipmentCode, string? Region, string Description, DateTime RaisedAt, DateTime? ResolvedAt, bool Active);
public sealed record CreateLogisticsEventRequest(string Type, string Severity, string? SupplierCode, string? ShipmentCode, string? Region, string Description);
