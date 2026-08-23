namespace Dspc.Application.Modules.Planning;

// Mirrors packages/contracts/planning-engine.yaml. All date-times are site-local wall clock (DateTimeKind.Unspecified).

public sealed class PlanningRequest
{
    public string ScenarioId { get; set; } = "";
    public string? BaselineId { get; set; }
    public DateOnly HorizonStart { get; set; }
    public DateOnly HorizonEnd { get; set; }
    public int TimeLimitMs { get; set; } = 2500;
    public List<PlanWorkCenter> WorkCenters { get; set; } = new();
    public List<PlanOrder> Orders { get; set; } = new();
    public List<MaterialAvailability> Materials { get; set; } = new();
    public ObjectiveWeights Weights { get; set; } = new();
}

public sealed class PlanWorkCenter
{
    public string Code { get; set; } = "";
    public string LineCode { get; set; } = "";
    public double HoursPerDay { get; set; } = 16;
    public double CapacityFactor { get; set; } = 1.0;
    public List<CalendarOverride> Calendar { get; set; } = new();
}

public sealed class CalendarOverride
{
    public DateOnly Date { get; set; }
    public double AvailableHours { get; set; }
}

public sealed class PlanOrder
{
    public string Code { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public int Priority { get; set; } = 3;
    public int Quantity { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public bool Frozen { get; set; }
    public string? LineCode { get; set; }
    public List<PlanOperation> Operations { get; set; } = new();
}

public sealed class PlanOperation
{
    public string Code { get; set; } = "";
    public int Sequence { get; set; }
    public string WorkCenterCode { get; set; } = "";
    public double DurationHours { get; set; }
    public bool Frozen { get; set; }
    public DateTime? BaselineStart { get; set; }
    public DateTime? BaselineEnd { get; set; }
    public List<MaterialRequirement> MaterialRequirements { get; set; } = new();
}

public sealed class MaterialRequirement
{
    public string PartCode { get; set; } = "";
    public decimal Quantity { get; set; }
}

public sealed class MaterialAvailability
{
    public string PartCode { get; set; } = "";
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public List<InboundSupply> Inbound { get; set; } = new();
}

public sealed class InboundSupply
{
    public decimal Quantity { get; set; }
    public DateOnly Eta { get; set; }
    public string? Reference { get; set; }
    public int? RiskScore { get; set; }
}

public sealed class ObjectiveWeights
{
    public double LatenessPerDayPerPriority { get; set; } = 10;
    public double ShortagePerUnit { get; set; } = 5;
    public double DowntimePerHour { get; set; } = 20;
    public double DeliveryBreachPerOrder { get; set; } = 100;
    public double ChangePerMovedOperation { get; set; } = 2;
    public double ChangeoverPerSwitch { get; set; } = 8;
}

public sealed class PlanningResponse
{
    public string Status { get; set; } = "FEASIBLE";
    public string Solver { get; set; } = "";
    public int ElapsedMs { get; set; }
    public ObjectiveBreakdown Objective { get; set; } = new();
    public List<ScheduledOperationResult> Operations { get; set; } = new();
    public List<OrderResult> Orders { get; set; } = new();
    public PlanKpi Kpi { get; set; } = new();
    public List<Explanation> Explanations { get; set; } = new();
}

public sealed class ObjectiveBreakdown
{
    public double Total { get; set; }
    public double Lateness { get; set; }
    public double Shortage { get; set; }
    public double Downtime { get; set; }
    public double DeliveryBreach { get; set; }
    public double Change { get; set; }
    public double Changeover { get; set; }
}

public sealed class ScheduledOperationResult
{
    public string OrderCode { get; set; } = "";
    public string OperationCode { get; set; } = "";
    public string WorkCenterCode { get; set; } = "";
    public string? LineCode { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool Changed { get; set; }
    public double ShiftDays { get; set; }
    public bool WaitingForMaterial { get; set; }
}

public sealed class OrderResult
{
    public string OrderCode { get; set; } = "";
    public string? LineCode { get; set; }
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedEnd { get; set; }
    public DateOnly DueDate { get; set; }
    public int LatenessDays { get; set; }
    public bool MaterialComplete { get; set; }
    public List<Shortage> Shortages { get; set; } = new();
}

public sealed class Shortage
{
    public string PartCode { get; set; } = "";
    public decimal Quantity { get; set; }
    public DateOnly? AvailableOn { get; set; }
}

public sealed class PlanKpi
{
    public double DowntimeHours { get; set; }
    public int LateOrders { get; set; }
    public int TotalLatenessDays { get; set; }
    public int MovedOperations { get; set; }
    public int OrdersWithShortage { get; set; }
    public double OnTimeRate { get; set; }
}

public sealed class Explanation
{
    public string ReasonCode { get; set; } = "";
    public string OrderCode { get; set; } = "";
    public Dictionary<string, object?> Params { get; set; } = new();
}

public static class ReasonCodes
{
    public const string OrderDelayedMaterialShortage = "ORDER_DELAYED_MATERIAL_SHORTAGE";
    public const string OrderPulledForward = "ORDER_PULLED_FORWARD";
    public const string OrderMovedLine = "ORDER_MOVED_LINE";
    public const string DowntimeReduced = "DOWNTIME_REDUCED";
    public const string OrderFrozenKept = "ORDER_FROZEN_KEPT";
    public const string OrderLateDue = "ORDER_LATE_DUE";
    public const string CapacityReduced = "CAPACITY_REDUCED";
    public const string FallbackUsed = "FALLBACK_USED";
    public const string NoChangeNeeded = "NO_CHANGE_NEEDED";
}
