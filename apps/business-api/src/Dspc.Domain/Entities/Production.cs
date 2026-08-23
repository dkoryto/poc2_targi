using Dspc.Domain.Common;

namespace Dspc.Domain.Entities;

public class ProductDefinition : Entity
{
    public string Code { get; set; } = "";
    public string NamePl { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string SerialPrefix { get; set; } = "";
    public string Family { get; set; } = "";
    public ICollection<BomVersion> BomVersions { get; set; } = new List<BomVersion>();
}

public class BomVersion : Entity
{
    public Guid ProductId { get; set; }
    public ProductDefinition? Product { get; set; }
    public string Version { get; set; } = "A";
    public bool IsActive { get; set; } = true;
    public DateOnly EffectiveFrom { get; set; }
    public ICollection<BomItem> Items { get; set; } = new List<BomItem>();
}

public class BomItem : Entity
{
    public Guid BomVersionId { get; set; }
    public Guid PartId { get; set; }
    public PartDefinition? Part { get; set; }
    public decimal QuantityPerUnit { get; set; }
    public int Sequence { get; set; }
    /// <summary>Operation sequence number that consumes the part (e.g. 30).</summary>
    public int ConsumedAtOperation { get; set; }
    public bool IsKeyComponent { get; set; }
}

public class AssemblyLine : Entity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid SiteId { get; set; }
}

public class WorkCenter : Entity
{
    public string Code { get; set; } = "";
    public string NamePl { get; set; } = "";
    public string NameEn { get; set; } = "";
    public Guid SiteId { get; set; }
    public Guid AssemblyLineId { get; set; }
    public AssemblyLine? AssemblyLine { get; set; }
    public double HoursPerDay { get; set; } = 16;
    public int ShiftStartHour { get; set; } = 6;
    public int Sequence { get; set; }
    public ICollection<CapacityCalendar> Calendar { get; set; } = new List<CapacityCalendar>();
}

public class CapacityCalendar : Entity
{
    public Guid WorkCenterId { get; set; }
    public DateOnly Date { get; set; }
    public double AvailableHours { get; set; }
    public string? Reason { get; set; }
}

public class ProductionOrder : VersionedEntity
{
    public string Code { get; set; } = "";
    public Guid ProductId { get; set; }
    public ProductDefinition? Product { get; set; }
    public Guid BomVersionId { get; set; }
    public BomVersion? BomVersion { get; set; }
    public Guid SiteId { get; set; }
    public Guid? AssemblyLineId { get; set; }
    public AssemblyLine? AssemblyLine { get; set; }
    public int Quantity { get; set; }
    public int Priority { get; set; } = 3;
    public DateOnly ReleaseDate { get; set; }
    public DateOnly DueDate { get; set; }
    public ProductionOrderStatus Status { get; set; }
    public bool Frozen { get; set; }
    public string? CustomerReference { get; set; }
    public ICollection<OperationDefinition> Operations { get; set; } = new List<OperationDefinition>();
    public ICollection<ProductSerial> Serials { get; set; } = new List<ProductSerial>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

public class OperationDefinition : Entity
{
    public Guid ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public string Code { get; set; } = "";
    public int Sequence { get; set; }
    public string NamePl { get; set; } = "";
    public string NameEn { get; set; } = "";
    public Guid WorkCenterId { get; set; }
    public WorkCenter? WorkCenter { get; set; }
    public double DurationHours { get; set; }
    public bool Frozen { get; set; }
    public OperationStatus Status { get; set; }
    /// <summary>JSON: [{"partCode":"ACT-40","quantity":8}]</summary>
    public string MaterialRequirementsJson { get; set; } = "[]";
}

public class PlanningBaseline : Entity
{
    public int Version { get; set; }
    public PlanningBaselineStatus Status { get; set; }
    public DateOnly HorizonStart { get; set; }
    public DateOnly HorizonEnd { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? SourceScenarioId { get; set; }
    public string? KpiJson { get; set; }
    public string? Notes { get; set; }
    public ICollection<ScheduledOperation> Operations { get; set; } = new List<ScheduledOperation>();
}

public class ScheduledOperation : Entity
{
    public Guid PlanningBaselineId { get; set; }
    public PlanningBaseline? PlanningBaseline { get; set; }
    public Guid OperationDefinitionId { get; set; }
    public OperationDefinition? Operation { get; set; }
    public Guid WorkCenterId { get; set; }
    public Guid? AssemblyLineId { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool Frozen { get; set; }
}

public class MaterialConsumption : Entity
{
    public Guid ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public Guid? OperationDefinitionId { get; set; }
    public Guid MaterialLotId { get; set; }
    public MaterialLot? MaterialLot { get; set; }
    public Guid? ProductSerialId { get; set; }
    public ProductSerial? ProductSerial { get; set; }
    public decimal Quantity { get; set; }
    public DateTime ConsumedAt { get; set; }
    public string RecordedBy { get; set; } = "";
}

public class ProductSerial : VersionedEntity
{
    public string SerialNumber { get; set; } = "";
    public Guid ProductId { get; set; }
    public ProductDefinition? Product { get; set; }
    public Guid ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public Guid BomVersionId { get; set; }
    public SerialStatus Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Passport? Passport { get; set; }
}

public class TraceabilityLink : Entity
{
    public TraceLinkKind Kind { get; set; }
    public string FromType { get; set; } = "";
    public Guid FromId { get; set; }
    public string FromCode { get; set; } = "";
    public string ToType { get; set; } = "";
    public Guid ToId { get; set; }
    public string ToCode { get; set; } = "";
}
