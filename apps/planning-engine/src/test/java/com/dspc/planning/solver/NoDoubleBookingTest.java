package com.dspc.planning.solver;

import com.dspc.planning.model.MaterialAvailability;
import com.dspc.planning.model.PlanOperation;
import com.dspc.planning.model.PlanOrder;
import com.dspc.planning.model.PlanningRequest;
import com.dspc.planning.model.PlanningResponse;
import com.dspc.planning.model.ScheduledOperation;
import com.dspc.planning.model.WorkCenter;
import org.junit.jupiter.api.Test;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.Comparator;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class NoDoubleBookingTest {

    @Test
    void competingOrdersWithoutBaselineAreSerialisedOnOneWorkCenter() {
        LocalDate t0 = Fixtures.T0;
        PlanOrder a = order("WO-A", 5, t0, t0.plusDays(5), 20);
        PlanOrder b = order("WO-B", 3, t0, t0.plusDays(5), 20);
        PlanOrder c = order("WO-C", 3, t0, t0.plusDays(4), 20);
        PlanningRequest req = new PlanningRequest("SCN-DOUBLE", null, t0, t0.plusDays(30), 2500,
                List.of(new WorkCenter("WC-X", "LINE-1", 16.0, 1.0, List.of())), List.of(a, b, c),
                List.of(new MaterialAvailability("P", 100, 0, List.of())), null);
        PlanningResponse r = Fixtures.solver().solve(req);
        PlanInvariants.assertHardConstraints(req, r);
        List<ScheduledOperation> ops = r.operations().stream().sorted(Comparator.comparing(ScheduledOperation::start)).toList();
        // priority 5 first, then due date, then code: A, C, B — strictly back-to-back
        assertThat(ops).extracting(ScheduledOperation::orderCode).containsExactly("WO-A", "WO-C", "WO-B");
        assertThat(ops.get(0).start()).isEqualTo(LocalDateTime.of(2026, 9, 7, 6, 0));
        assertThat(ops.get(1).start()).isEqualTo(ops.get(0).end());
        assertThat(ops.get(2).start()).isEqualTo(ops.get(1).end());
        assertThat(ops.get(2).end()).isEqualTo(LocalDateTime.of(2026, 9, 10, 18, 0)); // 60 h over 16 h days
    }

    @Test
    void pullForwardNeverOverlapsFrozenWork() {
        PlanningRequest req = Fixtures.load("act40-delay");
        PlanningResponse r = Fixtures.solver().solve(req);
        ScheduledOperation frozen = r.operations().stream().filter(o -> o.operationCode().equals("WO-2026-012/20")).findFirst().orElseThrow();
        for (ScheduledOperation o : r.operations()) {
            if (!o.workCenterCode().equals("WC-INT") || o == frozen) continue;
            boolean overlaps = o.start().isBefore(frozen.end()) && frozen.start().isBefore(o.end());
            assertThat(overlaps).as("%s overlaps frozen %s", o.operationCode(), frozen.operationCode()).isFalse();
        }
        PlanInvariants.assertHardConstraints(req, r);
    }

    private static PlanOrder order(String code, int prio, LocalDate release, LocalDate due, double hours) {
        return new PlanOrder(code, "PROD", prio, 1, due, release, false, "LINE-1",
                List.of(new PlanOperation(code + "/10", 10, "WC-X", hours, false, null, null, List.of())));
    }
}
