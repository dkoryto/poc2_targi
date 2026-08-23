using Dspc.Domain.Common;

namespace Dspc.Domain.Entities;

public class MaterialLot : VersionedEntity
{
    public string LotNumber { get; set; } = "";
    public string? HeatNumber { get; set; }
    public string? BatchNumber { get; set; }
    public Guid PartId { get; set; }
    public PartDefinition? Part { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid? PurchaseOrderLineId { get; set; }
    public PurchaseOrderLine? PurchaseOrderLine { get; set; }
    public decimal Quantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public string Unit { get; set; } = "szt";
    public MaterialLotStatus Status { get; set; }
    public DateOnly? ReceivedOn { get; set; }
    public DateOnly? ProducedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string CountryOfOrigin { get; set; } = "";
    public string? BlockReason { get; set; }
    public DateTime? BlockedAt { get; set; }
    public ICollection<QualityDocument> Documents { get; set; } = new List<QualityDocument>();
    public ICollection<QualityInspection> Inspections { get; set; } = new List<QualityInspection>();
}

public class InventoryBalance : VersionedEntity
{
    public Guid PartId { get; set; }
    public PartDefinition? Part { get; set; }
    public Guid SiteId { get; set; }
    public decimal OnHand { get; set; }
    public decimal Blocked { get; set; }
    public decimal Reserved { get; set; }
    public decimal Free => OnHand - Reserved;
}

public class Reservation : Entity
{
    public Guid PartId { get; set; }
    public PartDefinition? Part { get; set; }
    public Guid ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public Guid? MaterialLotId { get; set; }
    public MaterialLot? MaterialLot { get; set; }
    public decimal Quantity { get; set; }
    public bool IsBlocked { get; set; }
}

public class QualityDocument : VersionedEntity
{
    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; }
    public string DocumentNumber { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/pdf";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public DateOnly? IssuedOn { get; set; }
    public Guid? PurchaseOrderLineId { get; set; }
    public PurchaseOrderLine? PurchaseOrderLine { get; set; }
    public Guid? MaterialLotId { get; set; }
    public MaterialLot? MaterialLot { get; set; }
    public Guid? SupplierId { get; set; }
    public string? LotNumber { get; set; }
    public string? HeatNumber { get; set; }
    public string UploadedBy { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerificationComment { get; set; }
    public string? AiSuggestionJson { get; set; }
    public int Version { get; set; } = 1;
}

public class PassportTemplate : Entity
{
    public string Code { get; set; } = "DQP-01";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsDemo { get; set; } = true;
    public ICollection<QualityRequirement> Requirements { get; set; } = new List<QualityRequirement>();
}

public class QualityRequirement : Entity
{
    public Guid PassportTemplateId { get; set; }
    public string Code { get; set; } = "";
    public string TitlePl { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public int Sequence { get; set; }
    public bool Mandatory { get; set; } = true;
    public string? MappingNote { get; set; }
}

public class QualityInspection : Entity
{
    public string Code { get; set; } = "";
    public Guid? MaterialLotId { get; set; }
    public MaterialLot? MaterialLot { get; set; }
    public Guid? ProductSerialId { get; set; }
    public ProductSerial? ProductSerial { get; set; }
    public InspectionResult Result { get; set; }
    public string InspectedBy { get; set; } = "";
    public DateTime InspectedAt { get; set; }
    public string? Notes { get; set; }
    public string? MeasurementsJson { get; set; }
}

public class NonConformance : VersionedEntity
{
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public NonConformanceStatus Status { get; set; }
    public Guid? MaterialLotId { get; set; }
    public MaterialLot? MaterialLot { get; set; }
    public Guid? SupplierId { get; set; }
    public string RaisedBy { get; set; } = "";
    public DateTime RaisedAt { get; set; }
    public string? Disposition { get; set; }
}
