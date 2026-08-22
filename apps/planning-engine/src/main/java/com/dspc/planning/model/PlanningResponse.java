package com.dspc.planning.model;

import java.util.List;

public record PlanningResponse(
        PlanStatus status,
        String solver,
        long elapsedMs,
        ObjectiveBreakdown objective,
        List<ScheduledOperation> operations,
        List<OrderResult> orders,
        PlanKpi kpi,
        List<Explanation> explanations) {

    public PlanningResponse withElapsed(long ms) {
        return new PlanningResponse(status, solver, ms, objective, operations, orders, kpi, explanations);
    }
}
