package com.dspc.planning.solver;

import com.dspc.planning.model.MaterialRequirement;
import com.dspc.planning.model.Shortage;

import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Builds a complete schedule for a set of "flagged" orders (orderCode → lower bound for the first operation;
 * flagged orders ignore their baseline start, everything else is kept as close to baseline as possible).
 */
final class ScheduleBuilder {
    private final Problem problem;

    ScheduleBuilder(Problem problem) { this.problem = problem; }

    private record ReqItem(Problem.OrderCtx order, Problem.OpCtx op, MaterialRequirement req, boolean frozen, LocalDateTime desired) {}

    Schedule build(Map<String, LocalDateTime> flagged) {
        Schedule s = new Schedule(flagged);
        for (String wc : problem.calendars.keySet()) s.timelines.put(wc, new Timeline());

        // 1. create placeholders + pre-occupy frozen operations
        Map<String, Schedule.Placed> byOpCode = new LinkedHashMap<>();
        for (Problem.OrderCtx o : problem.orders) {
            List<Schedule.Placed> list = new ArrayList<>();
            for (Problem.OpCtx op : o.ops) {
                Schedule.Placed p = new Schedule.Placed(o, op);
                p.frozen = o.opFrozen(op);
                if (p.frozen) {
                    p.start = op.op.baselineStart();
                    p.end = op.op.baselineEnd();
                    Timeline tl = s.timelines.get(op.wc());
                    Timeline.Slot conflict = tl.firstConflict(p.start, p.end);
                    if (conflict != null)
                        s.infeasibleReasons.add("Frozen operations overlap on " + op.wc() + ": " + op.code() + " and " + conflict.operationCode());
                    tl.add(new Timeline.Slot(p.start, p.end, op.code(), o.order.productCodeOrEmpty()));
                }
                list.add(p);
                byOpCode.put(op.code(), p);
            }
            s.byOrder.put(o.code(), list);
        }

        // 2. material allocation in need-date order
        List<ReqItem> items = new ArrayList<>();
        for (Problem.OrderCtx o : problem.orders) {
            LocalDateTime flagL = flagged.get(o.code());
            for (Problem.OpCtx op : o.ops) {
                boolean frozen = o.opFrozen(op);
                LocalDateTime desired;
                if (frozen) desired = op.op.baselineStart();
                else if (flagL != null) desired = flagL;
                else desired = op.op.baselineStart() != null ? op.op.baselineStart() : o.releaseAt();
                if (desired.isBefore(o.releaseAt()) && !frozen) desired = o.releaseAt();
                for (MaterialRequirement r : op.requirements()) items.add(new ReqItem(o, op, r, frozen, desired));
            }
        }
        items.sort(Comparator
                .comparing((ReqItem i) -> !i.frozen())
                .thenComparing(ReqItem::desired)
                .thenComparing(Comparator.comparingInt((ReqItem i) -> i.order().order.priority()).reversed())
                .thenComparing(i -> i.order().order.dueDate())
                .thenComparing(i -> i.order().code())
                .thenComparingInt(i -> i.op().op.sequence())
                .thenComparing(i -> i.req().partCode()));
        MaterialLedger ledger = new MaterialLedger(problem.request.materials(), problem.horizonEnd);
        LocalDateTime horizonEndAt = problem.horizonEnd.atTime(WorkCalendar.DAY_START);
        for (ReqItem it : items) {
            MaterialLedger.Allocation a = ledger.allocate(it.req().partCode(), it.req().quantity());
            Schedule.Placed p = byOpCode.get(it.op().code());
            if (a.missingFromStock() > 0) {
                s.shortages.computeIfAbsent(it.order().code(), k -> new ArrayList<>())
                        .add(new Shortage(a.partCode(), round(a.missingFromStock()), a.coveredInHorizon() ? a.coveringEta() : null));
                if (!a.coveredInHorizon()) s.ordersWithUncoveredShortage.add(it.order().code());
            }
            LocalDateTime at = a.onHand() ? null : (a.coveredInHorizon() ? a.availableAt() : horizonEndAt);
            if (at != null && (p.materialAt == null || at.isAfter(p.materialAt))) {
                p.materialAt = at;
                p.bindingPart = a.partCode();
                p.bindingMissingQty = round(a.missingFromStock());
            }
        }

        // 3. placement: ranking order, operations in sequence
        for (Problem.OrderCtx o : problem.orders) {
            LocalDateTime flagL = flagged.get(o.code());
            LocalDateTime prevEnd = null;
            boolean first = true;
            for (Schedule.Placed p : s.byOrder.get(o.code())) {
                WorkCalendar cal = problem.calendar(p.op.wc());
                LocalDateTime bound = o.releaseAt();
                if (prevEnd != null && prevEnd.isAfter(bound)) bound = prevEnd;
                if (flagL != null) {
                    if (first && flagL.isAfter(bound)) bound = flagL;
                } else if (p.op.op.baselineStart() != null && p.op.op.baselineStart().isAfter(bound)) {
                    bound = p.op.op.baselineStart();
                }
                if (p.frozen) {
                    LocalDateTime required = bound;
                    if (p.materialAt != null && p.materialAt.isAfter(required)) required = p.materialAt;
                    if (required.isAfter(p.start)) s.frozenWouldMove.add(o.code());
                    if (prevEnd != null && prevEnd.isAfter(p.start))
                        s.infeasibleReasons.add("Frozen operation " + p.op.code() + " starts before its predecessor ends");
                } else {
                    LocalDateTime earliest = bound;
                    p.waitingForMaterial = p.materialAt != null && p.materialAt.isAfter(bound);
                    if (p.waitingForMaterial) earliest = p.materialAt;
                    Timeline tl = s.timelines.get(p.op.wc());
                    p.start = tl.findStart(earliest, p.op.minutes, cal);
                    p.end = cal.add(p.start, p.op.minutes);
                    tl.add(new Timeline.Slot(p.start, p.end, p.op.code(), o.order.productCodeOrEmpty()));
                }
                prevEnd = p.end;
                first = false;
                s.placed.add(p);
            }
        }
        return s;
    }

    private static double round(double v) { return Math.round(v * 1000.0) / 1000.0; }
}
