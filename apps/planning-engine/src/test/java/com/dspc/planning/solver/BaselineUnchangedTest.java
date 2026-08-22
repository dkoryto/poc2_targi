package com.dspc.planning.solver;

import com.dspc.planning.model.PlanStatus;
import com.dspc.planning.model.PlanningResponse;
import com.dspc.planning.model.ScheduledOperation;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class BaselineUnchangedTest {
    @Test
    void unchangedInputReproducesBaselineExactly() {
        PlanningResponse r = Fixtures.solve("baseline");
        assertThat(r.status()).isEqualTo(PlanStatus.FEASIBLE);
        assertThat(r.solver()).isEqualTo(HeuristicSolver.SOLVER_ID);
        assertThat(r.operations()).hasSize(29);
        assertThat(r.operations()).noneMatch(ScheduledOperation::changed);
        assertThat(r.operations()).allMatch(o -> o.shiftDays() == 0.0);
        assertThat(r.kpi().downtimeHours()).isZero();
        assertThat(r.kpi().movedOperations()).isZero();
        assertThat(r.kpi().lateOrders()).isZero();
        assertThat(r.objective().total()).isZero();
        assertThat(r.explanations()).isEmpty();
        // every requirement is covered inside the horizon (shortages only list inbound-dependent quantities)
        assertThat(r.kpi().ordersWithShortage()).isZero();
        assertThat(r.orders()).allSatisfy(o -> assertThat(o.shortages()).allMatch(s -> s.availableOn() != null));
        assertThat(r.orders()).filteredOn(o -> o.orderCode().equals("WO-2026-012")).singleElement()
                .satisfies(o -> assertThat(o.materialComplete()).isTrue());
    }
}
