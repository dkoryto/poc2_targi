package com.dspc.planning.solver;

import com.dspc.planning.model.Shortage;

import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/** Result of one deterministic construction pass. */
final class Schedule {
    static final class Placed {
        final Problem.OrderCtx order;
        final Problem.OpCtx op;
        LocalDateTime start, end;
        LocalDateTime materialAt;      // null = all material on hand
        boolean waitingForMaterial;    // start bounded by material availability
        boolean frozen;
        String bindingPart;            // part that determined materialAt
        double bindingMissingQty;

        Placed(Problem.OrderCtx order, Problem.OpCtx op) { this.order = order; this.op = op; }

        boolean changed() {
            if (!op.op.hasBaseline()) return true;
            return !start.equals(op.op.baselineStart()) || !end.equals(op.op.baselineEnd());
        }

        double shiftDays() {
            if (op.op.baselineStart() == null) return 0;
            long minutes = java.time.Duration.between(op.op.baselineStart(), start).toMinutes();
            return Math.round(minutes / 1440.0 * 10.0) / 10.0;
        }
    }

    final Map<String, LocalDateTime> flagged;
    final List<Placed> placed = new ArrayList<>();
    final Map<String, List<Placed>> byOrder = new LinkedHashMap<>();
    final Map<String, List<Shortage>> shortages = new LinkedHashMap<>();
    final Set<String> ordersWithUncoveredShortage = new LinkedHashSet<>();
    final Set<String> frozenWouldMove = new LinkedHashSet<>();
    final Map<String, Timeline> timelines = new LinkedHashMap<>();
    final List<String> infeasibleReasons = new ArrayList<>();

    Schedule(Map<String, LocalDateTime> flagged) { this.flagged = flagged; }

    boolean infeasible() { return !infeasibleReasons.isEmpty(); }

    List<Placed> ops(String orderCode) { return byOrder.getOrDefault(orderCode, List.of()); }

    LocalDateTime orderStart(String orderCode) {
        LocalDateTime min = null;
        for (Placed p : ops(orderCode)) if (min == null || p.start.isBefore(min)) min = p.start;
        return min;
    }

    LocalDateTime orderEnd(String orderCode) {
        LocalDateTime max = null;
        for (Placed p : ops(orderCode)) if (max == null || p.end.isAfter(max)) max = p.end;
        return max;
    }
}
