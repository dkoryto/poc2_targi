namespace Dspc.Application.Modules.Planning.Scheduling;

/// <summary>
/// Deterministic "baseline + current inputs, no re-sequencing" evaluation. Every operation is kept at its baseline
/// slot unless release date, predecessor, material availability or a higher-ranked operation forces it later.
/// Used for: dashboard predicted downtime, ETA-change impact, What-If "Before", and as the Heuristic fallback
/// when the Java engine is unavailable (it never pulls orders forward).
/// </summary>
public sealed class BaselineImpactEvaluator
{
    public const string SolverName = "Heuristic fallback";

    private sealed record Placed(string OrderCode, string OpCode, string ProductCode, DateTime Start, DateTime End);

    private sealed class Alloc
    {
        public decimal Covered;
        public DateTime? AvailableAt;   // null => available now
        public decimal Unmet;
        public decimal FromLaterInbound; // quantity covered by inbound arriving after the op's baseline start
        public DateOnly? LatestEta;
    }

    private sealed class PartTimeline
    {
        public decimal Free;
        public List<(decimal Qty, DateOnly Eta)> Inbound = new();
    }

    public PlanningResponse Evaluate(PlanningRequest req, string? solverName = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cal = new WorkCalendar(req.WorkCenters);
        var lineByWc = req.WorkCenters.ToDictionary(w => w.Code, w => w.LineCode, StringComparer.OrdinalIgnoreCase);

        var ranking = Rank(req.Orders);

        // 1. material allocation in ranking order
        var timeline = req.Materials.ToDictionary(m => m.PartCode, m => new PartTimeline
        {
            Free = Math.Max(0, m.OnHand - m.Reserved),
            Inbound = m.Inbound.OrderBy(i => i.Eta).ThenBy(i => i.Reference).Select(i => (i.Quantity, i.Eta)).ToList()
        }, StringComparer.OrdinalIgnoreCase);

        var opMaterial = new Dictionary<string, Dictionary<string, Alloc>>(); // opCode -> part -> alloc
        foreach (var order in ranking)
        {
            foreach (var op in order.Operations.OrderBy(o => o.Sequence))
            {
                var perPart = new Dictionary<string, Alloc>(StringComparer.OrdinalIgnoreCase);
                foreach (var reqm in op.MaterialRequirements.Where(r => r.Quantity > 0))
                {
                    var alloc = new Alloc();
                    if (!timeline.TryGetValue(reqm.PartCode, out var tl))
                    {
                        alloc.Unmet = reqm.Quantity;
                    }
                    else
                    {
                        var need = reqm.Quantity;
                        var fromFree = Math.Min(need, tl.Free);
                        tl.Free -= fromFree; need -= fromFree; alloc.Covered += fromFree;
                        for (var i = 0; i < tl.Inbound.Count && need > 0; i++)
                        {
                            var (qty, eta) = tl.Inbound[i];
                            if (qty <= 0) continue;
                            var take = Math.Min(need, qty);
                            tl.Inbound[i] = (qty - take, eta);
                            need -= take; alloc.Covered += take;
                            var at = eta.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Unspecified);
                            if (alloc.AvailableAt is null || at > alloc.AvailableAt) alloc.AvailableAt = at;
                            alloc.LatestEta = eta;
                            if (op.BaselineStart is { } bs && at > bs) alloc.FromLaterInbound += take;
                        }
                        alloc.Unmet = need;
                    }
                    perPart[reqm.PartCode] = alloc;
                }
                opMaterial[op.Code] = perPart;
            }
        }

        // 2. placement
        var occupied = req.WorkCenters.ToDictionary(w => w.Code, _ => new List<Placed>(), StringComparer.OrdinalIgnoreCase);
        var placedOps = new Dictionary<string, Placed>();
        var waiting = new HashSet<string>();
        var horizonEnd = req.HorizonEnd.AddDays(365).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        foreach (var order in ranking)
        {
            foreach (var op in order.Operations.OrderBy(o => o.Sequence))
            {
                if ((op.Frozen || order.Frozen) && op.BaselineStart is { } fs && op.BaselineEnd is { } fe)
                {
                    var p = new Placed(order.Code, op.Code, order.ProductCode, fs, fe);
                    occupied[op.WorkCenterCode].Add(p);
                    placedOps[op.Code] = p;
                }
            }
        }

        foreach (var order in ranking)
        {
            DateTime prevEnd = DateTime.MinValue;
            foreach (var op in order.Operations.OrderBy(o => o.Sequence))
            {
                if (placedOps.TryGetValue(op.Code, out var frozen)) { prevEnd = frozen.End; continue; }

                var release = order.ReleaseDate.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Unspecified);
                var earliest = release;
                if (prevEnd > earliest) earliest = prevEnd;
                if (op.BaselineStart is { } bs && bs > earliest) earliest = bs;
                var constraintNoMaterial = earliest;
                DateTime? materialAt = null;
                foreach (var a in opMaterial[op.Code].Values)
                {
                    if (a.Unmet > 0) { materialAt = horizonEnd; break; }
                    if (a.AvailableAt is { } at && (materialAt is null || at > materialAt)) materialAt = at;
                }
                if (materialAt is { } m && m > earliest) { earliest = m; if (m > constraintNoMaterial) waiting.Add(op.Code); }

                var wc = op.WorkCenterCode;
                var t = cal.NextWorkingTime(wc, earliest);
                DateTime end;
                for (var guard = 0; ; guard++)
                {
                    end = cal.AddWorkingHours(wc, t, op.DurationHours);
                    var conflict = occupied[wc].Where(o => o.Start < end && t < o.End).OrderBy(o => o.End).FirstOrDefault();
                    if (conflict is null || guard > 10000) break;
                    t = cal.NextWorkingTime(wc, conflict.End);
                }
                var placed = new Placed(order.Code, op.Code, order.ProductCode, t, end);
                occupied[wc].Add(placed);
                placedOps[op.Code] = placed;
                prevEnd = end;
            }
        }

        // 3. results
        var response = new PlanningResponse { Solver = solverName ?? SolverName, Status = "FEASIBLE" };
        double downtime = 0;
        int moved = 0;
        foreach (var order in req.Orders.OrderBy(o => o.Code))
        {
            foreach (var op in order.Operations.OrderBy(o => o.Sequence))
            {
                var p = placedOps[op.Code];
                var changed = op.BaselineStart is null || op.BaselineEnd is null || p.Start != op.BaselineStart || p.End != op.BaselineEnd;
                if (changed) moved++;
                var shift = op.BaselineStart is { } b ? Math.Round((p.Start - b).TotalDays, 1) : 0;
                response.Operations.Add(new ScheduledOperationResult
                {
                    OrderCode = order.Code, OperationCode = op.Code, WorkCenterCode = op.WorkCenterCode,
                    LineCode = lineByWc.GetValueOrDefault(op.WorkCenterCode), Start = p.Start, End = p.End,
                    Changed = changed, ShiftDays = shift, WaitingForMaterial = waiting.Contains(op.Code)
                });
                // predicted downtime = idle hours inside the baseline windows of operations that wait for material (knock-on moves excluded; same rule as the Java engine)
                if (op.BaselineStart is { } bs && op.BaselineEnd is { } be && !(op.Frozen || order.Frozen) && waiting.Contains(op.Code))
                {
                    var window = cal.WorkingHoursBetween(op.WorkCenterCode, bs, be);
                    double busy = 0;
                    foreach (var o in occupied[op.WorkCenterCode])
                    {
                        var os = o.Start > bs ? o.Start : bs;
                        var oe = o.End < be ? o.End : be;
                        if (oe > os) busy += cal.WorkingHoursBetween(op.WorkCenterCode, os, oe);
                    }
                    downtime += Math.Max(0, window - busy);
                }
            }
            var ops = order.Operations.OrderBy(o => o.Sequence).Select(o => placedOps[o.Code]).ToList();
            var start = ops.Min(o => o.Start);
            var end = ops.Max(o => o.End);
            var lateness = Math.Max(0, DateOnly.FromDateTime(end).DayNumber - order.DueDate.DayNumber);
            var shortages = new List<Shortage>();
            foreach (var op in order.Operations)
                foreach (var (part, a) in opMaterial[op.Code])
                {
                    var missing = a.Unmet + a.FromLaterInbound;
                    if (missing > 0)
                        shortages.Add(new Shortage { PartCode = part, Quantity = missing, AvailableOn = a.Unmet > 0 ? null : a.LatestEta });
                }
            response.Orders.Add(new OrderResult
            {
                OrderCode = order.Code, LineCode = order.LineCode ?? lineByWc.GetValueOrDefault(order.Operations.LastOrDefault()?.WorkCenterCode ?? ""),
                PlannedStart = start, PlannedEnd = end, DueDate = order.DueDate, LatenessDays = lateness,
                MaterialComplete = shortages.Count == 0, Shortages = shortages
            });
        }

        var late = response.Orders.Where(o => o.LatenessDays > 0).ToList();
        response.Kpi = new PlanKpi
        {
            DowntimeHours = Math.Round(downtime, 1),
            LateOrders = late.Count,
            TotalLatenessDays = late.Sum(o => o.LatenessDays),
            MovedOperations = moved,
            OrdersWithShortage = response.Orders.Count(o => !o.MaterialComplete),
            OnTimeRate = response.Orders.Count == 0 ? 1 : Math.Round(1.0 - (double)late.Count / response.Orders.Count, 3)
        };

        var w = req.Weights;
        var prio = req.Orders.ToDictionary(o => o.Code, o => o.Priority);
        var shortageUnits = response.Orders.SelectMany(o => o.Shortages.Where(s => s.AvailableOn is null)).Sum(s => (double)s.Quantity);
        var obj = new ObjectiveBreakdown
        {
            Lateness = late.Sum(o => o.LatenessDays * prio[o.OrderCode] * w.LatenessPerDayPerPriority),
            Shortage = shortageUnits * w.ShortagePerUnit,
            Downtime = response.Kpi.DowntimeHours * w.DowntimePerHour,
            DeliveryBreach = late.Count * w.DeliveryBreachPerOrder,
            Change = moved * w.ChangePerMovedOperation,
            Changeover = Math.Max(0, Changeovers(occupied) - BaselineChangeovers(req)) * w.ChangeoverPerSwitch
        };
        obj.Total = obj.Lateness + obj.Shortage + obj.Downtime + obj.DeliveryBreach + obj.Change + obj.Changeover;
        response.Objective = obj;

        // 4. explanations
        foreach (var order in ranking)
        {
            var res = response.Orders.First(o => o.OrderCode == order.Code);
            var boundOp = order.Operations.OrderBy(o => o.Sequence).FirstOrDefault(o => waiting.Contains(o.Code));
            if (boundOp is not null)
            {
                var alloc = opMaterial[boundOp.Code].Where(kv => kv.Value.Unmet > 0 || kv.Value.FromLaterInbound > 0)
                    .OrderByDescending(kv => kv.Value.Unmet).ThenByDescending(kv => kv.Value.AvailableAt).ThenBy(kv => kv.Key).FirstOrDefault();
                var opRes = response.Operations.First(o => o.OperationCode == boundOp.Code);
                response.Explanations.Add(new Explanation
                {
                    ReasonCode = ReasonCodes.OrderDelayedMaterialShortage, OrderCode = order.Code,
                    Params = new()
                    {
                        ["orderCode"] = order.Code,
                        ["partCode"] = alloc.Key,
                        ["missingQty"] = alloc.Value.Unmet + alloc.Value.FromLaterInbound,
                        ["days"] = res.LatenessDays > 0 ? res.LatenessDays : (int)Math.Ceiling(opRes.ShiftDays),
                        ["availableOn"] = alloc.Value.Unmet > 0 ? null : alloc.Value.LatestEta?.ToString("yyyy-MM-dd"),
                        ["operationCode"] = boundOp.Code
                    }
                });
            }
            if (res.LatenessDays > 0)
                response.Explanations.Add(new Explanation { ReasonCode = ReasonCodes.OrderLateDue, OrderCode = order.Code, Params = new() { ["orderCode"] = order.Code, ["days"] = res.LatenessDays, ["dueDate"] = order.DueDate.ToString("yyyy-MM-dd") } });
            if (order.Frozen || order.Operations.Any(o => o.Frozen))
                response.Explanations.Add(new Explanation { ReasonCode = ReasonCodes.OrderFrozenKept, OrderCode = order.Code, Params = new() { ["orderCode"] = order.Code } });
        }
        foreach (var wc in req.WorkCenters.Where(c => c.CapacityFactor < 1).OrderBy(c => c.Code))
            response.Explanations.Add(new Explanation { ReasonCode = ReasonCodes.CapacityReduced, OrderCode = "", Params = new() { ["workCenterCode"] = wc.Code, ["factor"] = wc.CapacityFactor } });

        response.ElapsedMs = (int)sw.ElapsedMilliseconds;
        return response;
    }

    /// <summary>
    /// Allocation/placement order — need date first (same rule as the Java engine): frozen orders, then desired start
    /// (baseline start of the first operation, else release date), then priority desc, due date asc, code.
    /// </summary>
    public static List<PlanOrder> Rank(IEnumerable<PlanOrder> orders) => orders
        .OrderByDescending(o => o.Frozen || o.Operations.Any(op => op.Frozen))
        .ThenBy(o => DesiredStart(o))
        .ThenByDescending(o => o.Priority)
        .ThenBy(o => o.DueDate)
        .ThenBy(o => o.Code, StringComparer.Ordinal)
        .ToList();

    private static DateTime DesiredStart(PlanOrder o)
    {
        var first = o.Operations.OrderBy(op => op.Sequence).FirstOrDefault();
        return first?.BaselineStart ?? o.ReleaseDate.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Unspecified);
    }

    private static int Changeovers(Dictionary<string, List<Placed>> occupied)
    {
        var n = 0;
        foreach (var list in occupied.Values)
        {
            string? prev = null;
            foreach (var p in list.OrderBy(p => p.Start))
            {
                if (prev is not null && prev != p.ProductCode) n++;
                prev = p.ProductCode;
            }
        }
        return n;
    }

    private static int BaselineChangeovers(PlanningRequest req)
    {
        var n = 0;
        foreach (var wcGroup in req.Orders.SelectMany(o => o.Operations.Where(op => op.BaselineStart is not null).Select(op => (o.ProductCode, op.WorkCenterCode, Start: op.BaselineStart!.Value)))
                     .GroupBy(x => x.WorkCenterCode))
        {
            string? prev = null;
            foreach (var p in wcGroup.OrderBy(p => p.Start))
            {
                if (prev is not null && prev != p.ProductCode) n++;
                prev = p.ProductCode;
            }
        }
        return n;
    }
}
