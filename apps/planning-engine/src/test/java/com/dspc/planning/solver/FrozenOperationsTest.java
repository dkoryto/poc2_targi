package com.dspc.planning.solver;

import com.dspc.planning.model.MaterialAvailability;
import com.dspc.planning.model.PlanningRequest;
import com.dspc.planning.model.PlanningResponse;
import com.dspc.planning.model.ScheduledOperation;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class FrozenOperationsTest {

    @Test
    void frozenOrderKeepsBaselineEvenWhenMaterialWouldPushIt() {
        PlanningRequest base = Fixtures.load("baseline");
        // remove all MCU-X7 stock: the frozen WO-2026-012 would have to wait for PO-2026-0009 (T0+25) if it were free to move
        List<MaterialAvailability> mats = new ArrayList<>();
        for (MaterialAvailability m : base.materials())
            mats.add(m.partCode().equals("MCU-X7") ? new MaterialAvailability("MCU-X7", 0, 0, m.inbound()) : m);
        PlanningRequest req = new PlanningRequest(base.scenarioId(), base.baselineId(), base.horizonStart(), base.horizonEnd(),
                base.timeLimitMs(), base.workCenters(), base.orders(), mats, base.weights());
        PlanningResponse r = Fixtures.solver().solve(req);

        for (ScheduledOperation op : r.operations()) {
            if (!op.orderCode().equals("WO-2026-012")) continue;
            assertThat(op.changed()).isFalse();
            assertThat(op.shiftDays()).isZero();
        }
        assertThat(r.operations()).filteredOn(o -> o.operationCode().equals("WO-2026-012/10")).singleElement()
                .satisfies(o -> assertThat(o.start()).isEqualTo(Fixtures.T0.atTime(6, 0)));
        assertThat(r.explanations()).anyMatch(e -> e.reasonCode().equals("ORDER_FROZEN_KEPT") && e.orderCode().equals("WO-2026-012"));
        // the frozen WO-2026-013/10 is kept too, while unfrozen MCU-X7 consumers move
        assertThat(r.operations()).filteredOn(o -> o.operationCode().equals("WO-2026-013/10")).singleElement()
                .satisfies(o -> assertThat(o.changed()).isFalse());
        assertThat(r.operations()).filteredOn(o -> o.operationCode().equals("WO-2026-014/30")).singleElement()
                .satisfies(o -> assertThat(o.waitingForMaterial()).isTrue());
        PlanInvariants.assertHardConstraints(req, r);
    }
}
