package com.dspc.planning.solver;

import com.dspc.planning.config.SolverProperties;
import com.dspc.planning.model.Explanation;
import com.dspc.planning.model.OrderResult;
import com.dspc.planning.model.PlanKpi;
import com.dspc.planning.model.PlanStatus;
import com.dspc.planning.model.PlanningRequest;
import com.dspc.planning.model.PlanningResponse;
import com.dspc.planning.model.ScheduledOperation;
import com.dspc.planning.model.Shortage;
import com.dspc.planning.model.WorkCenter;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.time.LocalDateTime;
import java.time.temporal.ChronoUnit;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TreeSet;

/**
 * dspc-heuristic/1.0 — deterministic list scheduling with a change-minimising baseline anchor and a
 * pull-forward improvement pass that fills capacity freed by material waits.
 */
@Service
public class HeuristicSolver {
    public static final String SOLVER_ID = "dspc-heuristic/1.0";
    private static final Logger log = LoggerFactory.getLogger(HeuristicSolver.class);
    private static final int MAX_IMPROVEMENT_ROUNDS = 100;

    private final SolverProperties props;

    public HeuristicSolver(SolverProperties props) { this.props = props; }

    public PlanningResponse solve(PlanningRequest request) {
        long t0 = System.nanoTime();
        Problem problem = new Problem(request);
        int timeLimit = request.timeLimitMs() == null ? props.defaultTimeLimit() : request.timeLimitMs();
        long deadline = t0 + timeLimit * 1_000_000L;
        ScheduleBuilder builder = new ScheduleBuilder(problem);
        ObjectiveCalculator objective = new ObjectiveCalculator(problem);

        Schedule initial = builder.build(Map.of());
        ObjectiveCalculator.Result initialObj = objective.evaluate(initial);
        List<Explanation> extra = new ArrayList<>();

        if (initial.infeasible()) {
            PlanningResponse r = assemble(problem, initial, initialObj, initialObj, PlanStatus.INFEASIBLE, extra);
            return r.withElapsed(elapsed(t0));
        }

        Schedule best = initial;
        ObjectiveCalculator.Result bestObj = initialObj;
        PlanStatus status = PlanStatus.FEASIBLE;

        if (timeLimit < props.minOptimiserBudget() || System.nanoTime() > deadline) {
            status = PlanStatus.FALLBACK;
            extra.add(new Explanation("FALLBACK_USED", "", Map.of("reason",
                    "timeLimitMs=" + timeLimit + " below optimiser budget; naive placement returned")));
        } else {
            Map<String, LocalDateTime> moved = new LinkedHashMap<>();
            for (int round = 0; round < MAX_IMPROVEMENT_ROUNDS; round++) {
                if (System.nanoTime() > deadline) break;
                Candidate c = bestCandidate(problem, builder, objective, best, bestObj, moved, deadline);
                if (c == null || c.obj.total() >= bestObj.total() - 1e-9) break;
                moved.put(c.orderCode, c.lowerBound);
                best = c.schedule;
                bestObj = c.obj;
            }
        }

        PlanningResponse r = assemble(problem, best, bestObj, initialObj, status, extra);
        long ms = elapsed(t0);
        log.info("scenarioId={} status={} elapsedMs={} objective={} moved={} downtimeH={} late={}",
                request.scenarioId(), status, ms, bestObj.total(), bestObj.movedOperations(),
                bestObj.downtimeMinutes() / 60.0, bestObj.lateOrders());
        return r.withElapsed(ms);
    }

    private record Candidate(String orderCode, LocalDateTime lowerBound, Schedule schedule, ObjectiveCalculator.Result obj) {}

    private Candidate bestCandidate(Problem problem, ScheduleBuilder builder, ObjectiveCalculator objective,
                                    Schedule current, ObjectiveCalculator.Result currentObj,
                                    Map<String, LocalDateTime> moved, long deadline) {
        // idle windows = baseline windows of material-waiting operations that still have idle time
        List<Schedule.Placed> windows = new ArrayList<>();
        for (Schedule.Placed p : current.placed)
            if (currentObj.idleMinutesByOp().containsKey(p.op.code())) windows.add(p);
        windows.sort(Comparator.comparing((Schedule.Placed p) -> p.op.op.baselineStart()).thenComparing(p -> p.op.code()));

        Candidate best = null;
        for (Problem.OrderCtx o : problem.orders) {
            if (o.hasFrozenOps() || moved.containsKey(o.code())) continue;
            Set<LocalDateTime> bounds = new TreeSet<>();
            bounds.add(o.releaseAt());
            for (Schedule.Placed w : windows) {
                for (int k = 0; k < o.ops.size(); k++) {
                    Problem.OpCtx op = o.ops.get(k);
                    if (!op.wc().equals(w.op.wc())) continue;
                    LocalDateTime t = w.op.op.baselineStart();
                    for (int j = k - 1; j >= 0; j--) t = problem.calendar(o.ops.get(j).wc()).subtract(t, o.ops.get(j).minutes);
                    if (t.isBefore(o.releaseAt())) t = o.releaseAt();
                    bounds.add(t);
                }
            }
            for (LocalDateTime L : bounds) {
                if (System.nanoTime() > deadline) return best;
                Map<String, LocalDateTime> trial = new LinkedHashMap<>(moved);
                trial.put(o.code(), L);
                Schedule s = builder.build(trial);
                if (s.infeasible()) continue;
                ObjectiveCalculator.Result r = objective.evaluate(s);
                if (best == null || r.total() < best.obj.total() - 1e-9) best = new Candidate(o.code(), L, s, r);
            }
        }
        return best;
    }

    private PlanningResponse assemble(Problem problem, Schedule s, ObjectiveCalculator.Result obj,
                                      ObjectiveCalculator.Result initialObj, PlanStatus status, List<Explanation> extra) {
        List<ScheduledOperation> ops = new ArrayList<>();
        List<OrderResult> orders = new ArrayList<>();
        List<Problem.OrderCtx> byCode = new ArrayList<>(problem.orders);
        byCode.sort(Comparator.comparing(Problem.OrderCtx::code));
        for (Problem.OrderCtx o : byCode) {
            for (Schedule.Placed p : s.ops(o.code())) {
                ops.add(new ScheduledOperation(o.code(), p.op.code(), p.op.wc(), problem.lineOf(p.op.wc()),
                        p.start, p.end, !p.frozen && p.changed(), p.frozen ? 0 : p.shiftDays(), p.waitingForMaterial));
            }
            List<Shortage> sh = new ArrayList<>(s.shortages.getOrDefault(o.code(), List.of()));
            sh.sort(Comparator.comparing(Shortage::partCode));
            orders.add(new OrderResult(o.code(), problem.lineOf(o), s.orderStart(o.code()), s.orderEnd(o.code()),
                    o.order.dueDate(), obj.latenessByOrder().getOrDefault(o.code(), 0), sh.isEmpty(), sh));
        }
        int n = problem.orders.size();
        PlanKpi kpi = new PlanKpi(obj.downtimeMinutes() / 60.0, obj.lateOrders(), obj.totalLatenessDays(),
                obj.movedOperations(), obj.ordersWithShortage(), n == 0 ? 1.0 : Math.round((n - obj.lateOrders()) * 1000.0 / n) / 1000.0);
        List<Explanation> explanations = explain(problem, s, obj, initialObj);
        explanations.addAll(extra);
        return new PlanningResponse(status, SOLVER_ID, 0, obj.breakdown(), ops, orders, kpi, explanations);
    }

    private static final List<String> REASON_ORDER = List.of(
            "ORDER_DELAYED_MATERIAL_SHORTAGE", "ORDER_PULLED_FORWARD", "ORDER_MOVED_LINE", "DOWNTIME_REDUCED",
            "ORDER_LATE_DUE", "ORDER_FROZEN_KEPT", "CAPACITY_REDUCED", "FALLBACK_USED");

    private List<Explanation> explain(Problem problem, Schedule s, ObjectiveCalculator.Result obj, ObjectiveCalculator.Result initialObj) {
        List<Explanation> out = new ArrayList<>();
        List<Problem.OrderCtx> byCode = new ArrayList<>(problem.orders);
        byCode.sort(Comparator.comparing(Problem.OrderCtx::code));
        for (Problem.OrderCtx o : byCode) {
            List<Schedule.Placed> placed = s.ops(o.code());
            int late = obj.latenessByOrder().getOrDefault(o.code(), 0);
            for (Schedule.Placed p : placed) {
                if (p.frozen || !p.waitingForMaterial || !p.op.op.hasBaseline() || !p.start.isAfter(p.op.op.baselineStart())) continue;
                Map<String, Object> params = new LinkedHashMap<>();
                params.put("orderCode", o.code());
                params.put("partCode", p.bindingPart);
                params.put("missingQty", p.bindingMissingQty);
                params.put("days", late > 0 ? late : (int) Math.ceil(p.shiftDays()));
                params.put("availableOn", p.materialAt == null ? null : p.materialAt.toLocalDate().toString());
                out.add(new Explanation("ORDER_DELAYED_MATERIAL_SHORTAGE", o.code(), params));
                break;
            }
            LocalDateTime baseFirst = o.baselineFirstStart();
            LocalDateTime newFirst = s.orderStart(o.code());
            if (s.flagged.containsKey(o.code()) && baseFirst != null && newFirst != null && newFirst.isBefore(baseFirst)) {
                Set<String> wcs = new LinkedHashSet<>();
                for (Schedule.Placed p : placed) wcs.add(p.op.wc());
                int reqs = 0, onHand = 0;
                for (Problem.OpCtx op : o.ops) for (var r : op.requirements()) { reqs++; }
                List<Shortage> sh = s.shortages.getOrDefault(o.code(), List.of());
                onHand = Math.max(0, reqs - sh.size());
                Map<String, Object> params = new LinkedHashMap<>();
                params.put("orderCode", o.code());
                params.put("lineCode", problem.lineOf(o));
                params.put("days", (int) ChronoUnit.DAYS.between(newFirst.toLocalDate(), baseFirst.toLocalDate()));
                params.put("materialCompleteness", reqs == 0 ? 1.0 : Math.round(onHand * 100.0 / reqs) / 100.0);
                params.put("workCenters", new ArrayList<>(wcs));
                out.add(new Explanation("ORDER_PULLED_FORWARD", o.code(), params));
            }
            if (late > 0) {
                Map<String, Object> params = new LinkedHashMap<>();
                params.put("orderCode", o.code());
                params.put("days", late);
                out.add(new Explanation("ORDER_LATE_DUE", o.code(), params));
            }
            if (s.frozenWouldMove.contains(o.code())) {
                out.add(new Explanation("ORDER_FROZEN_KEPT", o.code(), Map.of("orderCode", o.code())));
            }
        }
        if (obj.downtimeMinutes() < initialObj.downtimeMinutes()) {
            Map<String, Object> params = new LinkedHashMap<>();
            params.put("fromHours", initialObj.downtimeMinutes() / 60.0);
            params.put("toHours", obj.downtimeMinutes() / 60.0);
            out.add(new Explanation("DOWNTIME_REDUCED", "", params));
        }
        for (WorkCenter wc : problem.workCenters.values()) {
            if (wc.capacityFactorOrDefault() < 1.0) {
                Map<String, Object> params = new LinkedHashMap<>();
                params.put("workCenterCode", wc.code());
                params.put("factor", wc.capacityFactorOrDefault());
                out.add(new Explanation("CAPACITY_REDUCED", "", params));
            }
        }
        out.sort(Comparator.comparingInt((Explanation e) -> REASON_ORDER.indexOf(e.reasonCode())).thenComparing(Explanation::orderCode));
        return out;
    }

    private static long elapsed(long t0) { return (System.nanoTime() - t0) / 1_000_000L; }
}
