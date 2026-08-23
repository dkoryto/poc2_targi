using Dspc.Application.Abstractions;
using Dspc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dspc.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierPerformance> SupplierPerformances => Set<SupplierPerformance>();
    public DbSet<PartDefinition> Parts => Set<PartDefinition>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PurchaseOrderLineChange> PurchaseOrderLineChanges => Set<PurchaseOrderLineChange>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();
    public DbSet<LogisticsRiskEvent> LogisticsRiskEvents => Set<LogisticsRiskEvent>();
    public DbSet<MaterialLot> MaterialLots => Set<MaterialLot>();
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<QualityDocument> QualityDocuments => Set<QualityDocument>();
    public DbSet<PassportTemplate> PassportTemplates => Set<PassportTemplate>();
    public DbSet<QualityRequirement> QualityRequirements => Set<QualityRequirement>();
    public DbSet<QualityInspection> QualityInspections => Set<QualityInspection>();
    public DbSet<NonConformance> NonConformances => Set<NonConformance>();
    public DbSet<ProductDefinition> Products => Set<ProductDefinition>();
    public DbSet<BomVersion> BomVersions => Set<BomVersion>();
    public DbSet<BomItem> BomItems => Set<BomItem>();
    public DbSet<AssemblyLine> AssemblyLines => Set<AssemblyLine>();
    public DbSet<WorkCenter> WorkCenters => Set<WorkCenter>();
    public DbSet<CapacityCalendar> CapacityCalendars => Set<CapacityCalendar>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<OperationDefinition> OperationDefinitions => Set<OperationDefinition>();
    public DbSet<PlanningBaseline> PlanningBaselines => Set<PlanningBaseline>();
    public DbSet<ScheduledOperation> ScheduledOperations => Set<ScheduledOperation>();
    public DbSet<MaterialConsumption> MaterialConsumptions => Set<MaterialConsumption>();
    public DbSet<ProductSerial> ProductSerials => Set<ProductSerial>();
    public DbSet<TraceabilityLink> TraceabilityLinks => Set<TraceabilityLink>();
    public DbSet<Passport> Passports => Set<Passport>();
    public DbSet<PassportVersion> PassportVersions => Set<PassportVersion>();
    public DbSet<PlanningScenario> PlanningScenarios => Set<PlanningScenario>();
    public DbSet<ScenarioChange> ScenarioChanges => Set<ScenarioChange>();
    public DbSet<PlanningRecommendation> PlanningRecommendations => Set<PlanningRecommendation>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnake(entity.GetTableName()!));
            foreach (var p in entity.GetProperties()) p.SetColumnName(ToSnake(p.GetColumnName()));
            foreach (var k in entity.GetKeys()) k.SetName(ToSnake(k.GetName()!));
            foreach (var fk in entity.GetForeignKeys()) fk.SetConstraintName(ToSnake(fk.GetConstraintName()!));
            foreach (var ix in entity.GetIndexes()) ix.SetDatabaseName(ToSnake(ix.GetDatabaseName()!));
        }
    }

    public static string ToSnake(string name)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]) && name[i - 1] != '_'))) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }
}

public sealed class DesignTimeDbContextFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? "Host=localhost;Port=5432;Database=dspc;Username=dspc;Password=dspc";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs, o => o.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)).Options;
        return new AppDbContext(options);
    }
}
