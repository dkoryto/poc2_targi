package com.dspc.planning.solver;

import com.dspc.planning.model.MaterialAvailability;
import com.dspc.planning.model.PlanningRequest;
import com.dspc.planning.model.PlanningResponse;
import com.dspc.planning.model.ScheduledOperation;
import com.dspc.planning.model.WorkCenter;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class HardConstraintsTest {

    @ParameterizedTest
    @ValueSource(strings = {"baseline", "act40-delay"})
    void fixturesRespectAllHardConstraints(String name) {
        PlanningRequest req = Fixtures.load(name);
        PlanInvariants.assertHardConstraints(req, Fixtures.solver().solve(req));
    }

    @Test
    void operationNeverStartsBeforeMaterialArrives() {
        PlanningResponse r = Fixtures.solve("act40-delay");
        // ACT-40 for WO-2026-014 / WO-2026-015 arrives with PO-2026-0007/1 on T0+18
        for (String code : List.of("WO-2026-014/30", "WO-2026-015/30")) {
            ScheduledOperation op = r.operations().stream().filter(o -> o.operationCode().equals(code)).findFirst().orElseThrow();
            assertThat(op.start()).isAfterOrEqualTo(Fixtures.day(18).atTime(6, 0));
        }
    }

    @Test
    void reducedCapacityStillYieldsValidPlan() {
        PlanningRequest base = Fixtures.load("act40-delay");
        List<WorkCenter> wcs = new ArrayList<>();
        for (WorkCenter wc : base.workCenters())
            wcs.add(wc.code().equals("WC-INT") ? new WorkCenter(wc.code(), wc.lineCode(), wc.hoursPerDay(), 0.5, wc.calendar()) : wc);
        PlanningRequest req = new PlanningRequest(base.scenarioId() + "-CAP50", base.baselineId(), base.horizonStart(), base.horizonEnd(),
                base.timeLimitMs(), wcs, base.orders(), base.materials(), base.weights());
        PlanningResponse r = Fixtures.solver().solve(req);
        PlanInvariants.assertHardConstraints(req, r);
        assertThat(r.explanations()).anyMatch(e -> e.reasonCode().equals("CAPACITY_REDUCED") && e.params().get("workCenterCode").equals("WC-INT"));
        // 8 h/day on WC-INT: WO-2026-013/30 (32 h, baseline 2 days) must now stretch over 4 working days
        ScheduledOperation op = r.operations().stream().filter(o -> o.operationCode().equals("WO-2026-013/30")).findFirst().orElseThrow();
        assertThat(op.end()).isAfter(op.start().plusDays(3));
    }

    @Test
    void uncoveredMaterialIsReportedAsShortageNotIgnored() {
        PlanningRequest base = Fixtures.load("baseline");
        List<MaterialAvailability> mats = new ArrayList<>();
        for (MaterialAvailability m : base.materials())
            mats.add(m.partCode().equals("OPT-12") ? new MaterialAvailability("OPT-12", 0, 0, List.of()) : m);
        PlanningRequest req = new PlanningRequest(base.scenarioId(), base.baselineId(), base.horizonStart(), base.horizonEnd(),
                base.timeLimitMs(), base.workCenters(), base.orders(), mats, base.weights());
        PlanningResponse r = Fixtures.solver().solve(req);
        PlanInvariants.assertHardConstraints(req, r);
        assertThat(r.kpi().ordersWithShortage()).isEqualTo(2); // WO-2026-014 and WO-2026-016 use OPT-12
        assertThat(r.objective().shortage()).isGreaterThan(0);
        assertThat(r.orders()).filteredOn(o -> o.orderCode().equals("WO-2026-014")).singleElement()
                .satisfies(o -> assertThat(o.shortages()).anyMatch(s -> s.partCode().equals("OPT-12") && s.availableOn() == null));
    }
}
