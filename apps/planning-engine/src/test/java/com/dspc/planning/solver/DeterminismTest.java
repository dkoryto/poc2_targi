package com.dspc.planning.solver;

import com.dspc.planning.model.PlanningRequest;
import com.dspc.planning.model.PlanningResponse;
import com.fasterxml.jackson.core.JsonProcessingException;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class DeterminismTest {

    @Test
    void sameInputGivesIdenticalOutput() throws JsonProcessingException {
        PlanningRequest req = Fixtures.load("act40-delay");
        PlanningResponse a = Fixtures.solver().solve(req).withElapsed(0);
        PlanningResponse b = Fixtures.solver().solve(req).withElapsed(0);
        PlanningResponse c = Fixtures.solver().solve(Fixtures.load("act40-delay")).withElapsed(0);
        String ja = Fixtures.MAPPER.writeValueAsString(a);
        assertThat(Fixtures.MAPPER.writeValueAsString(b)).isEqualTo(ja);
        assertThat(Fixtures.MAPPER.writeValueAsString(c)).isEqualTo(ja);
        assertThat(a).isEqualTo(b);
    }

    @Test
    void orderOfInputListsDoesNotMatter() throws JsonProcessingException {
        PlanningRequest base = Fixtures.load("act40-delay");
        var orders = new java.util.ArrayList<>(base.orders());
        java.util.Collections.reverse(orders);
        var wcs = new java.util.ArrayList<>(base.workCenters());
        java.util.Collections.reverse(wcs);
        PlanningRequest shuffled = new PlanningRequest(base.scenarioId(), base.baselineId(), base.horizonStart(), base.horizonEnd(),
                base.timeLimitMs(), wcs, orders, base.materials(), base.weights());
        String a = Fixtures.MAPPER.writeValueAsString(Fixtures.solver().solve(base).withElapsed(0));
        String b = Fixtures.MAPPER.writeValueAsString(Fixtures.solver().solve(shuffled).withElapsed(0));
        assertThat(b).isEqualTo(a);
    }
}
