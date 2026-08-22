package com.dspc.planning.solver;

import com.dspc.planning.model.PlanStatus;
import com.dspc.planning.model.PlanningRequest;
import com.dspc.planning.model.PlanningResponse;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class FallbackTimeLimitTest {

    @Test
    void tinyTimeLimitReturnsNaivePlacementFlaggedAsFallback() {
        PlanningRequest base = Fixtures.load("act40-delay");
        PlanningRequest req = new PlanningRequest(base.scenarioId(), base.baselineId(), base.horizonStart(), base.horizonEnd(),
                1, base.workCenters(), base.orders(), base.materials(), base.weights());
        PlanningResponse r = Fixtures.solver().solve(req);
        assertThat(r.status()).isEqualTo(PlanStatus.FALLBACK);
        assertThat(r.explanations()).anyMatch(e -> e.reasonCode().equals("FALLBACK_USED"));
        // still a valid plan: WO-2026-014 delayed by material, nothing pulled forward
        assertThat(r.kpi().downtimeHours()).isEqualTo(36.0);
        assertThat(r.orders()).filteredOn(o -> o.orderCode().equals("WO-2026-014")).singleElement()
                .satisfies(o -> assertThat(o.latenessDays()).isEqualTo(4));
        assertThat(r.explanations()).noneMatch(e -> e.reasonCode().equals("ORDER_PULLED_FORWARD"));
        PlanInvariants.assertHardConstraints(req, r);
    }
}
