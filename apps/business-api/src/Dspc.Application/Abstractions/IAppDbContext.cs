using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<Site> Sites { get; }
    DbSet<User> Users { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierPerformance> SupplierPerformances { get; }
    DbSet<PartDefinition> Parts { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<PurchaseOrderLineChange> PurchaseOrderLineChanges { get; }
    DbSet<Shipment> Shipments { get; }
    DbSet<ShipmentEvent> ShipmentEvents { get; }
    DbSet<LogisticsRiskEvent> LogisticsRiskEvents { get; }
    DbSet<MaterialLot> MaterialLots { get; }
    DbSet<InventoryBalance> InventoryBalances { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<QualityDocument> QualityDocuments { get; }
    DbSet<PassportTemplate> PassportTemplates { get; }
    DbSet<QualityRequirement> QualityRequirements { get; }
    DbSet<QualityInspection> QualityInspections { get; }
    DbSet<NonConformance> NonConformances { get; }
    DbSet<ProductDefinition> Products { get; }
    DbSet<BomVersion> BomVersions { get; }
    DbSet<BomItem> BomItems { get; }
    DbSet<AssemblyLine> AssemblyLines { get; }
    DbSet<WorkCenter> WorkCenters { get; }
    DbSet<CapacityCalendar> CapacityCalendars { get; }
    DbSet<ProductionOrder> ProductionOrders { get; }
    DbSet<OperationDefinition> OperationDefinitions { get; }
    DbSet<PlanningBaseline> PlanningBaselines { get; }
    DbSet<ScheduledOperation> ScheduledOperations { get; }
    DbSet<MaterialConsumption> MaterialConsumptions { get; }
    DbSet<ProductSerial> ProductSerials { get; }
    DbSet<TraceabilityLink> TraceabilityLinks { get; }
    DbSet<Passport> Passports { get; }
    DbSet<PassportVersion> PassportVersions { get; }
    DbSet<PlanningScenario> PlanningScenarios { get; }
    DbSet<ScenarioChange> ScenarioChanges { get; }
    DbSet<PlanningRecommendation> PlanningRecommendations { get; }
    DbSet<RiskAssessment> RiskAssessments { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditEvent> AuditEvents { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
