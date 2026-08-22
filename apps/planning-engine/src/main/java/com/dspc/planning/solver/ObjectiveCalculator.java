package com.dspc.planning.solver;

import com.dspc.planning.model.ObjectiveBreakdown;
import com.dspc.planning.model.ObjectiveWeights;
import com.dspc.planning.model.PlanOperation;
import com.dspc.planning.model.Shortage;

import java.time.LocalDateTime;
import java.time.temporal.ChronoUnit;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

final class ObjectiveCalculator {
    record Result(ObjectiveBreakdown breakdown, long downtimeMinutes, int lateOrders, int totalLatenessDays,
                  int movedOperations, int ordersWithShortage, Map<String, Integer> latenessByOrder,
                  Map<String, Long> idleMinutesByOp) {
        double total() { return breakdown.total(); }
    }

    private final Problem problem;

    ObjectiveCalculator(Problem problem) { this.problem = problem; }

    Result evaluate(Schedule s) {
        ObjectiveWeights w = problem.weights;
        double lateness = 0, shortage = 0, deliveryBreach = 0, change = 0, changeover = 0;
        int lateOrders = 0, totalLate = 0, moved = 0;
        Map<String, Integer> latenessByOrder = new LinkedHashMap<>();

        for (Problem.OrderCtx o : problem.orders) {
            LocalDateTime end = s.orderEnd(o.code());
            int late = end == null ? 0 : (int) Math.max(0, ChronoUnit.DAYS.between(o.order.dueDate(), end.toLocalDate()));
            latenessByOrder.put(o.code(), late);
            if (late > 0) {
                lateOrders++;
                totalLate += late;
                lateness += late * o.order.priority() * w.lateness();
                deliveryBreach += w.deliveryBreach();
            }
            for (Shortage sh : s.shortages.getOrDefault(o.code(), List.of()))
                if (sh.availableOn() == null) shortage += sh.quantity() * w.shortage();
        }
        for (Schedule.Placed p : s.placed) if (!p.frozen && p.changed()) { moved++; change += w.change(); }

        // downtime: idle working minutes inside the baseline window of operations that are waiting for material
        Map<String, Long> idleByOp = new LinkedHashMap<>();
        long downtimeMinutes = 0;
        for (Schedule.Placed p : s.placed) {
            PlanOperation op = p.op.op;
            if (!p.waitingForMaterial || !op.hasBaseline()) continue;
            WorkCalendar cal = problem.calendar(p.op.wc());
            long window = cal.between(op.baselineStart(), op.baselineEnd());
            long busy = 0;
            for (Timeline.Slot slot : s.timelines.get(p.op.wc()).slots()) {
                LocalDateTime a = slot.start().isAfter(op.baselineStart()) ? slot.start() : op.baselineStart();
                LocalDateTime b = slot.end().isBefore(op.baselineEnd()) ? slot.end() : op.baselineEnd();
                if (b.isAfter(a)) busy += cal.between(a, b);
            }
            long idle = Math.max(0, window - busy);
            if (idle > 0) idleByOp.put(op.code(), idle);
            downtimeMinutes += idle;
        }
        double downtime = downtimeMinutes / 60.0 * w.downtime();

        // changeovers: product switches per work center vs. baseline sequence
        for (String wc : problem.calendars.keySet()) {
            List<Timeline.Slot> now = new ArrayList<>(s.timelines.get(wc).slots());
            int after = switches(now.stream().map(Timeline.Slot::productCode).toList());
            List<Schedule.Placed> base = new ArrayList<>();
            for (Schedule.Placed p : s.placed) if (p.op.wc().equals(wc) && p.op.op.hasBaseline()) base.add(p);
            base.sort(Comparator.comparing((Schedule.Placed p) -> p.op.op.baselineStart()).thenComparing(p -> p.op.code()));
            int before = switches(base.stream().map(p -> p.order.order.productCodeOrEmpty()).toList());
            if (after > before) changeover += (after - before) * w.changeover();
        }

        double total = lateness + shortage + downtime + deliveryBreach + change + changeover;
        ObjectiveBreakdown b = new ObjectiveBreakdown(r(total), r(lateness), r(shortage), r(downtime), r(deliveryBreach), r(change), r(changeover));
        int withShortage = s.ordersWithUncoveredShortage.size();
        return new Result(b, downtimeMinutes, lateOrders, totalLate, moved, withShortage, latenessByOrder, idleByOp);
    }

    private static int switches(List<String> products) {
        int n = 0;
        for (int i = 1; i < products.size(); i++) if (!products.get(i).equals(products.get(i - 1))) n++;
        return n;
    }

    private static double r(double v) { return Math.round(v * 100.0) / 100.0; }
}
