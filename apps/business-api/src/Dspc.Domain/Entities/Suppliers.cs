using Dspc.Domain.Common;

namespace Dspc.Domain.Entities;

public class Supplier : VersionedEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    public string City { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    /// <summary>Last 90 days on-time-in-full, 0..100, maintained from SupplierPerformance rows.</summary>
    public double OtifPercent { get; set; }
    public double QualityScore { get; set; } = 90;
    public bool IsActive { get; set; } = true;
    public ICollection<SupplierPerformance> Performance { get; set; } = new List<SupplierPerformance>();
}

public class SupplierPerformance : Entity
{
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int DeliveredLines { get; set; }
    public int OnTimeInFullLines { get; set; }
    public int QualityRejections { get; set; }
    public double OtifPercent { get; set; }
}

public class PartDefinition : Entity
{
    public string Code { get; set; } = "";
    public string NamePl { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Unit { get; set; } = "szt";
    public int Criticality { get; set; } = 3;
    public PartCategory Category { get; set; }
    public bool HasAlternativeSupplier { get; set; }
    public Guid? PrimarySupplierId { get; set; }
    public Supplier? PrimarySupplier { get; set; }
    public Guid? AlternativeSupplierId { get; set; }
    /// <summary>JSON array of DocumentType names required per delivered lot.</summary>
    public string RequiredDocumentTypesJson { get; set; } = "[]";
}

public class PurchaseOrder : VersionedEntity
{
    public string Code { get; set; } = "";
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid SiteId { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public DateOnly OrderedOn { get; set; }
    public string? Notes { get; set; }
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

public class PurchaseOrderLine : VersionedEntity
{
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public int LineNo { get; set; }
    public Guid PartId { get; set; }
    public PartDefinition? Part { get; set; }
    public decimal Quantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public DateOnly RequiredDate { get; set; }
    public DateOnly Eta { get; set; }
    public DateOnly OriginalEta { get; set; }
    public int ProgressPercent { get; set; }
    public PurchaseOrderLineStatus Status { get; set; }
    public string? LotNumber { get; set; }
    public string? HeatNumber { get; set; }
    public DateOnly? ProducedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public bool SupplierConfirmed { get; set; } = true;
    public DateOnly? DeliveredOn { get; set; }
    public Guid? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
    public int RiskScore { get; set; }
    public RiskCategory RiskCategory { get; set; }
    public string? LastComment { get; set; }
    public ICollection<QualityDocument> Documents { get; set; } = new List<QualityDocument>();
    public ICollection<PurchaseOrderLineChange> History { get; set; } = new List<PurchaseOrderLineChange>();
}

public class PurchaseOrderLineChange : Entity
{
    public Guid PurchaseOrderLineId { get; set; }
    public string Field { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangedBy { get; set; } = "";
    public string? Comment { get; set; }
}

public class Shipment : VersionedEntity
{
    public string Code { get; set; } = "";
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public ShipmentStatus Status { get; set; }
    public string? Carrier { get; set; }
    public string? Vehicle { get; set; }
    public DateTime? PlannedDeparture { get; set; }
    public DateTime? ActualDeparture { get; set; }
    public DateOnly Eta { get; set; }
    public DateTime? ArrivedAt { get; set; }
    /// <summary>0..1 progress along route, updated by events.</summary>
    public double Progress { get; set; }
    public ICollection<ShipmentEvent> Events { get; set; } = new List<ShipmentEvent>();
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

public class ShipmentEvent : Entity
{
    public Guid ShipmentId { get; set; }
    public ShipmentEventType Type { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Note { get; set; }
    public string? Location { get; set; }
    public string RecordedBy { get; set; } = "";
}

public class LogisticsRiskEvent : Entity
{
    public string Code { get; set; } = "";
    public LogisticsEventType Type { get; set; }
    public EventSeverity Severity { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? ShipmentId { get; set; }
    public string? Region { get; set; }
    public string Description { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsActive => ResolvedAt is null;
}
