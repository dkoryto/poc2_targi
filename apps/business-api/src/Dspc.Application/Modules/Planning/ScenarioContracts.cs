using Dspc.Domain.Common;

namespace Dspc.Application.Modules.Planning;

// Wire shapes consumed by apps/web/src/api/types.ts (ScenarioPreset, PlanningScenario, ScenarioCompare, …).

/// <summary>One scenario change as sent by the web app: a discriminated union keyed by <c>type</c>.</summary>
public sealed record ScenarioChangeDto(
    ScenarioChangeType Type,
    Guid? PoLineId = null,
    int? Days = null,
    string? LotNumber = null,
    string? OrderCode = null,
    int? Priority = null,
    string? WorkCenterCode = null,
    double? Factor = null,
    string? PoCode = null,
    string? PartCode = null);

public sealed record ScenarioPresetDto(string Key, string TitleKey, IReadOnlyList<ScenarioChangeDto> Changes, bool Featured = false);

public sealed record CreateScenarioRequest(string Name, IReadOnlyList<ScenarioChangeDto> Changes, string? PresetKey = null, string? SiteCode = null);

public sealed record ConsequenceDto(string Kind, string? TextKey, IReadOnlyDictionary<string, object?>? Params = null, string? Text = null);

public sealed record ScenarioDto(
    Guid Id,
    string Name,
    string Status,
    DateTime CreatedAt,
    string CreatedBy,
    IReadOnlyList<ScenarioChangeDto> Changes,
    string? PresetKey,
    string? Solver,
    int? ElapsedMs,
    GanttData? Before,
    GanttData? After,
    PlanKpi? KpiBefore,
    PlanKpi? KpiAfter,
    IReadOnlyList<Explanation>? Explanations,
    IReadOnlyList<ConsequenceDto>? Consequences,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    int? BaselineVersion,
    string? ErrorMessage,
    /// <summary>
    /// Operations whose window differs from the <em>approved baseline</em>. Distinct from
    /// <c>KpiAfter.MovedOperations</c>, which counts what re-planning moved relative to "before".
    /// </summary>
    int? ChangesVsBaseline = null);

public sealed record ScenarioSummaryDto(
    Guid Id,
    string Name,
    string Status,
    DateTime CreatedAt,
    string CreatedBy,
    string? Solver,
    int ChangeCount,
    PlanKpi? KpiAfter,
    string? PresetKey);

public sealed record MovedOperationDto(
    string OperationCode,
    string OrderCode,
    string WorkCenterCode,
    OperationWindow Before,
    OperationWindow After,
    double ShiftDays);

public sealed record OperationWindow(DateTime Start, DateTime End);

public sealed record ScenarioCompareDto(IReadOnlyList<MovedOperationDto> MovedOperations, PlanKpi KpiDelta);

public sealed record ScenarioRunAcceptedDto(Guid Id, string Status);
