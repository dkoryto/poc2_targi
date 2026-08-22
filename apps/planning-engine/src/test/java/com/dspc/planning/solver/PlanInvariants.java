package com.dspc.planning.solver;

import com.dspc.planning.model.PlanOperation;
import com.dspc.planning.model.PlanOrder;
import com.dspc.planning.model.PlanningRequest;
import com.dspc.planning.model.PlanningResponse;
import com.dspc.planning.model.ScheduledOperation;
import com.dspc.planning.model.WorkCenter;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import static org.assertj.core.api.Assertions.assertThat;

/** Hard-constraint checks shared by several tests. */
final class PlanInvariants {
    private PlanInvariants() {}

    static void assertHardConstraints(PlanningRequest req, PlanningResponse res) {
        Map<String, WorkCalendar> cals = new HashMap<>();
        for (WorkCenter wc : req.workCenters())
            cals.put(wc.code(), new WorkCalendar(wc.hoursPerDayOrDefault(), wc.capacityFactorOrDefault(), wc.calendar()));
        Map<String, PlanOrder> orders = new HashMap<>();
        Map<String, PlanOperation> opsByCode = new HashMap<>();
        for (PlanOrder o : req.orders()) { orders.put(o.code(), o); o.operations().forEach(op -> opsByCode.put(op.code(), op)); }

        assertThat(res.operations()).hasSize(opsByCode.size());

        // no overlap per work center
        Map<String, List<ScheduledOperation>> byWc = new HashMap<>();
        for (ScheduledOperation op : res.operations()) byWc.computeIfAbsent(op.workCenterCode(), k -> new ArrayList<>()).add(op);
        for (var e : byWc.entrySet()) {
            List<ScheduledOperation> list = e.getValue();
            list.sort(Comparator.comparing(ScheduledOperation::start));
            for (int i = 1; i < list.size(); i++)
                assertThat(list.get(i).start()).as("overlap on %s between %s and %s", e.getKey(), list.get(i - 1).operationCode(), list.get(i).operationCode())
                        .isAfterOrEqualTo(list.get(i - 1).end());
        }

        for (ScheduledOperation op : res.operations()) {
            PlanOperation def = opsByCode.get(op.operationCode());
            PlanOrder order = orders.get(op.orderCode());
            WorkCalendar cal = cals.get(op.workCenterCode());
            assertThat(op.workCenterCode()).isEqualTo(def.workCenterCode());
            boolean frozen = order.isFrozen() || def.isFrozen();
            if (!frozen) {
                // duration preserved in working time and placed inside working windows
                assertThat(cal.between(op.start(), op.end())).as("duration of %s", op.operationCode()).isEqualTo(Math.round(def.durationHours() * 60));
                assertThat(cal.next(op.start())).isEqualTo(op.start());
            }
            // release date
            assertThat(op.start().toLocalDate()).as("release of %s", op.operationCode()).isAfterOrEqualTo(order.releaseDate());
            // frozen kept
            if (frozen) {
                assertThat(op.start()).isEqualTo(def.baselineStart());
                assertThat(op.end()).isEqualTo(def.baselineEnd());
                assertThat(op.changed()).isFalse();
            }
        }

        // sequence inside each order
        for (PlanOrder order : req.orders()) {
            List<ScheduledOperation> chain = res.operations().stream().filter(o -> o.orderCode().equals(order.code()))
                    .sorted(Comparator.comparingInt(o -> opsByCode.get(o.operationCode()).sequence())).toList();
            for (int i = 1; i < chain.size(); i++)
                assertThat(chain.get(i).start()).as("sequence in %s", order.code()).isAfterOrEqualTo(chain.get(i - 1).end());
        }
    }
}
