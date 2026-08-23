using Dspc.Domain.Common;

namespace Dspc.Domain.Entities;

public class PlanningScenario : VersionedEntity
{
    public string Name { get; set; } = "";
    public Guid SiteId { get; set; }
    public string? PresetKey { get; set; }
    public PlanningScenarioStatus Status { get; set; }
    public Guid BaselineId { get; set; }
    public PlanningBaseline? Baseline { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? KpiBeforeJson { get; set; }
    public string? KpiAfterJson { get; set; }
    public string? ExplanationsJson { get; set; }
    public string? Solver { get; set; }
    public int? ElapsedMs { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public ICollection<ScenarioChange> Changes { get; set; } = new List<ScenarioChange>();
    public ICollection<PlanningRecommendation> Recommendations { get; set; } = new List<PlanningRecommendation>();
}

public class ScenarioChange : Entity
{
    public Guid PlanningScenarioId { get; set; }
    public ScenarioChangeType Type { get; set; }
    public string? TargetCode { get; set; }
    public Guid? TargetId { get; set; }
    public string ParametersJson { get; set; } = "{}";
    public int Sequence { get; set; }
}

public class PlanningRecommendation : Entity
{
    public Guid PlanningScenarioId { get; set; }
    public string ReasonCode { get; set; } = "";
    public string? OrderCode { get; set; }
    public string ParamsJson { get; set; } = "{}";
    public int Sequence { get; set; }
}

public class RiskAssessment : Entity
{
    public Guid PurchaseOrderLineId { get; set; }
    public PurchaseOrderLine? PurchaseOrderLine { get; set; }
    public int Score { get; set; }
    public RiskCategory Category { get; set; }
    public int? PreviousScore { get; set; }
    public string FactorsJson { get; set; } = "[]";
    public string EndangeredOrdersJson { get; set; } = "[]";
    public string Trigger { get; set; } = "";
    public DateTime AssessedAt { get; set; }
}

public class Notification : Entity
{
    public Guid? UserId { get; set; }
    public Role? TargetRole { get; set; }
    public NotificationSeverity Severity { get; set; }
    public string TitleKey { get; set; } = "";
    public string MessageKey { get; set; } = "";
    public string ParamsJson { get; set; } = "{}";
    public string? Route { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class AuditEvent
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public string UserName { get; set; } = "";
    public string? UserRole { get; set; }
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string EntityCode { get; set; } = "";
    public Guid? EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string CorrelationId { get; set; } = "";
    public AuditSource Source { get; set; }
    public string? IpAddress { get; set; }
}

public class OutboxMessage
{
    public long Id { get; set; }
    public string EventName { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public string EventType { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextAttemptAt { get; set; }
}

public class IdempotencyRecord
{
    public string Key { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
