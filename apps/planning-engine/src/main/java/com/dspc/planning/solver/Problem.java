package com.dspc.planning.solver;

import com.dspc.planning.model.ObjectiveWeights;
import com.dspc.planning.model.PlanOperation;
import com.dspc.planning.model.PlanOrder;
import com.dspc.planning.model.PlanningRequest;
import com.dspc.planning.model.WorkCenter;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Immutable, validated view of a request with derived helpers. */
final class Problem {
    static final class OpCtx {
        final PlanOperation op;
        final long minutes;
        final int index;
        OpCtx(PlanOperation op, int index) {
            this.op = op;
            this.index = index;
            this.minutes = Math.round(op.durationHours() * 60);
        }
        String code() { return op.code(); }
        String wc() { return op.workCenterCode(); }
        java.util.List<com.dspc.planning.model.MaterialRequirement> requirements() { return op.requirements(); }
    }

    static final class OrderCtx {
        final PlanOrder order;
        final List<OpCtx> ops = new ArrayList<>();
        final int rank;
        OrderCtx(PlanOrder order, int rank) {
            this.order = order;
            this.rank = rank;
            List<PlanOperation> sorted = new ArrayList<>(order.operations());
            sorted.sort(Comparator.comparingInt(PlanOperation::sequence).thenComparing(PlanOperation::code));
            for (int i = 0; i < sorted.size(); i++) ops.add(new OpCtx(sorted.get(i), i));
        }
        String code() { return order.code(); }
        boolean isFrozen() { return order.isFrozen(); }
        boolean opFrozen(OpCtx o) { return order.isFrozen() || o.op.isFrozen(); }
        boolean hasFrozenOps() { return order.isFrozen() || ops.stream().anyMatch(o -> o.op.isFrozen()); }
        LocalDateTime releaseAt() { return order.releaseDate().atTime(WorkCalendar.DAY_START); }
        LocalDateTime baselineFirstStart() {
            LocalDateTime min = null;
            for (OpCtx o : ops) if (o.op.baselineStart() != null && (min == null || o.op.baselineStart().isBefore(min))) min = o.op.baselineStart();
            return min;
        }
    }

    static final Comparator<PlanOrder> RANKING = Comparator
            .comparing((PlanOrder o) -> !o.isFrozen())
            .thenComparing(Comparator.comparingInt(PlanOrder::priority).reversed())
            .thenComparing(PlanOrder::dueDate)
            .thenComparing(PlanOrder::code);

    final PlanningRequest request;
    final Map<String, WorkCalendar> calendars = new LinkedHashMap<>();
    final Map<String, WorkCenter> workCenters = new LinkedHashMap<>();
    final List<OrderCtx> orders = new ArrayList<>();
    final Map<String, OrderCtx> ordersByCode = new LinkedHashMap<>();
    final LocalDate horizonStart, horizonEnd;
    final ObjectiveWeights weights;

    Problem(PlanningRequest request) {
        this.request = request;
        this.horizonStart = request.horizonStart();
        this.horizonEnd = request.horizonEnd();
        this.weights = request.weightsOrDefault();
        List<WorkCenter> wcs = new ArrayList<>(request.workCenters());
        wcs.sort(Comparator.comparing(WorkCenter::code));
        for (WorkCenter wc : wcs) {
            if (workCenters.put(wc.code(), wc) != null) throw new IllegalArgumentException("Duplicate work center " + wc.code());
            calendars.put(wc.code(), new WorkCalendar(wc.hoursPerDayOrDefault(), wc.capacityFactorOrDefault(), wc.calendar()));
        }
        List<PlanOrder> sorted = new ArrayList<>(request.orders());
        sorted.sort(RANKING);
        for (int i = 0; i < sorted.size(); i++) {
            OrderCtx ctx = new OrderCtx(sorted.get(i), i);
            if (ordersByCode.put(ctx.code(), ctx) != null) throw new IllegalArgumentException("Duplicate order " + ctx.code());
            orders.add(ctx);
            for (OpCtx o : ctx.ops) {
                if (!workCenters.containsKey(o.wc()))
                    throw new IllegalArgumentException("Operation " + o.code() + " references unknown work center " + o.wc());
                if (ctx.opFrozen(o) && !o.op.hasBaseline())
                    throw new IllegalArgumentException("Frozen operation " + o.code() + " requires baselineStart and baselineEnd");
                if (o.op.hasBaseline() && !o.op.baselineEnd().isAfter(o.op.baselineStart()))
                    throw new IllegalArgumentException("Operation " + o.code() + " has baselineEnd before baselineStart");
            }
        }
        if (!horizonEnd.isAfter(horizonStart)) throw new IllegalArgumentException("horizonEnd must be after horizonStart");
    }

    WorkCalendar calendar(String wc) { return calendars.get(wc); }

    String lineOf(String wc) {
        WorkCenter w = workCenters.get(wc);
        return w == null || w.lineCode() == null ? "" : w.lineCode();
    }

    String lineOf(OrderCtx o) {
        if (o.order.lineCode() != null && !o.order.lineCode().isBlank()) return o.order.lineCode();
        OpCtx longest = null;
        for (OpCtx op : o.ops) if (longest == null || op.minutes > longest.minutes) longest = op;
        return longest == null ? "" : lineOf(longest.wc());
    }
}
