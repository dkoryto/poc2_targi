using Dspc.Domain.Common;

namespace Dspc.Application.Modules.Planning;

/// <summary>Pure scenario maths, kept out of <see cref="ScenarioService"/> so it can be unit-tested without a database.</summary>
public static class ScenarioCalculations
{
    /// <summary>Maps scenario changes onto planning-model overrides. <paramref name="etaByLine"/> holds the current ETA per purchase-order line.</summary>
    public static PlanOverrides BuildOverrides(IEnumerable<ScenarioChangeDto> changes, IReadOnlyDictionary<Guid, DateOnly> etaByLine)
    {
        var o = new PlanOverrides();
        foreach (var c in changes)
        {
            switch (c.Type)
            {
                case ScenarioChangeType.DELAY_INBOUND when c.PoLineId is { } id && c.Days is { } days && etaByLine.TryGetValue(id, out var eta):
                    o.EtaByLineId[id] = eta.AddDays(days);
                    break;
                case ScenarioChangeType.BLOCK_LOT when !string.IsNullOrWhiteSpace(c.LotNumber):
                    o.BlockedLots.Add(c.LotNumber);
                    break;
                case ScenarioChangeType.PRIORITY when !string.IsNullOrWhiteSpace(c.OrderCode) && c.Priority is { } p:
                    o.PriorityByOrder[c.OrderCode] = p;
                    break;
                case ScenarioChangeType.CAPACITY when !string.IsNullOrWhiteSpace(c.WorkCenterCode) && c.Factor is { } f:
                    o.CapacityFactorByWorkCenter[c.WorkCenterCode] = f;
                    break;
                case ScenarioChangeType.DELAY_ORDER when !string.IsNullOrWhiteSpace(c.OrderCode) && c.Days is { } dd:
                    o.DelayDaysByOrder[c.OrderCode] = dd;
                    break;
            }
        }
        return o;
    }

    /// <summary>Operations whose window or work centre differs between the two plans, ordered by new start.</summary>
    public static List<MovedOperationDto> MovedOperations(GanttData before, GanttData after)
    {
        var beforeOps = before.Operations.ToDictionary(o => o.Code, StringComparer.OrdinalIgnoreCase);
        var moved = new List<MovedOperationDto>();
        foreach (var a in after.Operations.OrderBy(o => o.Start).ThenBy(o => o.Code, StringComparer.Ordinal))
        {
            if (!beforeOps.TryGetValue(a.Code, out var b)) continue;
            if (b.Start == a.Start && b.End == a.End && b.WorkCenterCode == a.WorkCenterCode) continue;
            moved.Add(new MovedOperationDto(a.Code, a.OrderCode, a.WorkCenterCode,
                new OperationWindow(b.Start, b.End), new OperationWindow(a.Start, a.End),
                Math.Round((a.Start - b.Start).TotalDays, 1)));
        }
        return moved;
    }

    /// <summary>After − Before. Negative downtime/lateness means the proposal is an improvement.</summary>
    public static PlanKpi KpiDelta(PlanKpi before, PlanKpi after) => new()
    {
        DowntimeHours = Math.Round(after.DowntimeHours - before.DowntimeHours, 2),
        LateOrders = after.LateOrders - before.LateOrders,
        TotalLatenessDays = after.TotalLatenessDays - before.TotalLatenessDays,
        MovedOperations = after.MovedOperations - before.MovedOperations,
        OrdersWithShortage = after.OrdersWithShortage - before.OrdersWithShortage,
        OnTimeRate = Math.Round(after.OnTimeRate - before.OnTimeRate, 4)
    };
}
