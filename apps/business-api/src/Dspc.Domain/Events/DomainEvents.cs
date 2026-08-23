using Dspc.Domain.Common;

namespace Dspc.Domain.Events;

public static class EventNames
{
    public const string SupplierOrderStatusChanged = "SupplierOrderStatusChanged";
    public const string ShipmentEtaChanged = "ShipmentEtaChanged";
    public const string QualityDocumentUploaded = "QualityDocumentUploaded";
    public const string QualityDocumentVerified = "QualityDocumentVerified";
    public const string MaterialLotBlocked = "MaterialLotBlocked";
    public const string DeliveryRiskChanged = "DeliveryRiskChanged";
    public const string PlanningScenarioCompleted = "PlanningScenarioCompleted";
    public const string ProductionPlanApproved = "ProductionPlanApproved";
    public const string PassportInvalidated = "PassportInvalidated";
    public const string PassportGenerated = "PassportGenerated";
    public const string LogisticsRiskEventRaised = "LogisticsRiskEventRaised";
    public const string ShipmentEventRecorded = "ShipmentEventRecorded";
    public const string NotificationCreated = "NotificationCreated";
    public const string DemoReset = "DemoReset";
}

public sealed record SupplierOrderStatusChanged(DateTime OccurredAt, string CorrelationId, string PoCode, Guid LineId, int LineNo, string PartCode, string SupplierCode, string OldStatus, string NewStatus, int ProgressPercent)
    : DomainEventBase(EventNames.SupplierOrderStatusChanged, OccurredAt, CorrelationId);

public sealed record ShipmentEtaChanged(DateTime OccurredAt, string CorrelationId, string PoCode, Guid LineId, int LineNo, string PartCode, string SupplierCode, DateOnly OldEta, DateOnly NewEta, DateOnly RequiredDate, string? Reason)
    : DomainEventBase(EventNames.ShipmentEtaChanged, OccurredAt, CorrelationId);

public sealed record QualityDocumentUploaded(DateTime OccurredAt, string CorrelationId, Guid DocumentId, string Type, string? PoCode, Guid? LineId, string? LotNumber, string SupplierCode)
    : DomainEventBase(EventNames.QualityDocumentUploaded, OccurredAt, CorrelationId);

public sealed record QualityDocumentVerified(DateTime OccurredAt, string CorrelationId, Guid DocumentId, string Status, Guid? LineId, string? LotNumber)
    : DomainEventBase(EventNames.QualityDocumentVerified, OccurredAt, CorrelationId);

public sealed record MaterialLotBlocked(DateTime OccurredAt, string CorrelationId, string LotNumber, string PartCode, string Reason, string[] AffectedOrders, string[] AffectedSerials)
    : DomainEventBase(EventNames.MaterialLotBlocked, OccurredAt, CorrelationId);

public sealed record DeliveryRiskChanged(DateTime OccurredAt, string CorrelationId, string PoCode, Guid LineId, int LineNo, string PartCode, string SupplierCode, int OldScore, int NewScore, string OldCategory, string NewCategory, string[] EndangeredOrders)
    : DomainEventBase(EventNames.DeliveryRiskChanged, OccurredAt, CorrelationId);

public sealed record PlanningScenarioCompleted(DateTime OccurredAt, string CorrelationId, Guid ScenarioId, string Status, string Solver, int ElapsedMs)
    : DomainEventBase(EventNames.PlanningScenarioCompleted, OccurredAt, CorrelationId);

public sealed record ProductionPlanApproved(DateTime OccurredAt, string CorrelationId, Guid ScenarioId, int BaselineVersion, string ApprovedBy)
    : DomainEventBase(EventNames.ProductionPlanApproved, OccurredAt, CorrelationId);

public sealed record PassportInvalidated(DateTime OccurredAt, string CorrelationId, string SerialNumber, string Reason)
    : DomainEventBase(EventNames.PassportInvalidated, OccurredAt, CorrelationId);

public sealed record PassportGenerated(DateTime OccurredAt, string CorrelationId, string SerialNumber, int Version, string Sha256)
    : DomainEventBase(EventNames.PassportGenerated, OccurredAt, CorrelationId);

public sealed record LogisticsRiskEventRaised(DateTime OccurredAt, string CorrelationId, string Code, string Type, string Severity, string? SupplierCode, string? ShipmentCode, string Description)
    : DomainEventBase(EventNames.LogisticsRiskEventRaised, OccurredAt, CorrelationId);

public sealed record ShipmentEventRecorded(DateTime OccurredAt, string CorrelationId, string ShipmentCode, string Type, string Status, double Progress)
    : DomainEventBase(EventNames.ShipmentEventRecorded, OccurredAt, CorrelationId);

public sealed record NotificationCreated(DateTime OccurredAt, string CorrelationId, Guid NotificationId, string? TargetRole, string Severity, string TitleKey)
    : DomainEventBase(EventNames.NotificationCreated, OccurredAt, CorrelationId);

public sealed record DemoReset(DateTime OccurredAt, string CorrelationId, long DurationMs, string SeedVersion)
    : DomainEventBase(EventNames.DemoReset, OccurredAt, CorrelationId);
