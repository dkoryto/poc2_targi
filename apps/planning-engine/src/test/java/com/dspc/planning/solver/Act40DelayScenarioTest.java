package com.dspc.planning.solver;

import com.dspc.planning.model.Explanation;
import com.dspc.planning.model.OrderResult;
import com.dspc.planning.model.PlanStatus;
import com.dspc.planning.model.PlanningResponse;
import com.dspc.planning.model.ScheduledOperation;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;

import java.time.LocalDateTime;

import static org.assertj.core.api.Assertions.assertThat;

/** Scenario "Opóźnij siłowniki ACT-40 o 10 dni" — PO-2026-0007/1 ETA T0+8 → T0+18. */
class Act40DelayScenarioTest {
    static PlanningResponse r;

    @BeforeAll
    static void solve() { r = Fixtures.solve("act40-delay"); }

    @Test
    void isFeasibleAndFast() {
        assertThat(r.status()).isEqualTo(PlanStatus.FEASIBLE);
        assertThat(r.elapsedMs()).isLessThan(3000);
    }

    @Test
    void wo014IsLateByFourDaysBecauseOfAct40() {
        OrderResult wo014 = order("WO-2026-014");
        assertThat(wo014.latenessDays()).isEqualTo(4);
        assertThat(wo014.materialComplete()).isFalse();
        assertThat(wo014.shortages()).singleElement().satisfies(s -> {
            assertThat(s.partCode()).isEqualTo("ACT-40");
            assertThat(s.quantity()).isEqualTo(8.0);
            assertThat(s.availableOn()).isEqualTo(Fixtures.day(18));
        });
        ScheduledOperation inte = op("WO-2026-014/30");
        assertThat(inte.start()).isEqualTo(Fixtures.day(18).atTime(6, 0));
        assertThat(inte.waitingForMaterial()).isTrue();
        assertThat(inte.changed()).isTrue();
        assertThat(inte.shiftDays()).isEqualTo(9.0);
    }

    @Test
    void wo019IsPulledIntoFreedIntegrationSlot() {
        ScheduledOperation inte = op("WO-2026-019/20");
        assertThat(inte.workCenterCode()).isEqualTo("WC-INT");
        assertThat(inte.start().toLocalDate()).isEqualTo(Fixtures.day(9));
        assertThat(inte.start()).isEqualTo(LocalDateTime.of(2026, 9, 16, 14, 0));
        assertThat(inte.end()).isEqualTo(LocalDateTime.of(2026, 9, 18, 10, 0));
        assertThat(op("WO-2026-019/10").start()).isEqualTo(LocalDateTime.of(2026, 9, 15, 6, 0));
        assertThat(order("WO-2026-019").latenessDays()).isZero();
        assertThat(order("WO-2026-019").materialComplete()).isTrue();
    }

    @Test
    void downtimeDropsFrom36To8Hours() {
        assertThat(r.kpi().downtimeHours()).isEqualTo(8.0);
        Explanation e = explanation("DOWNTIME_REDUCED", "");
        assertThat(e.params()).containsEntry("fromHours", 36.0).containsEntry("toHours", 8.0);
    }

    @Test
    void explanationsAreDeterministicReasonCodes() {
        Explanation pulled = explanation("ORDER_PULLED_FORWARD", "WO-2026-019");
        assertThat(pulled.params()).containsEntry("lineCode", "LINE-2").containsEntry("days", 29)
                .containsEntry("materialCompleteness", 1.0);
        @SuppressWarnings("unchecked") java.util.List<String> wcs = (java.util.List<String>) pulled.params().get("workCenters");
        assertThat(wcs).contains("WC-ELEC", "WC-INT");

        Explanation delayed = explanation("ORDER_DELAYED_MATERIAL_SHORTAGE", "WO-2026-014");
        assertThat(delayed.params()).containsEntry("partCode", "ACT-40").containsEntry("missingQty", 8.0)
                .containsEntry("days", 4).containsEntry("availableOn", Fixtures.day(18).toString());

        assertThat(explanation("ORDER_LATE_DUE", "WO-2026-014").params()).containsEntry("days", 4);
        assertThat(r.explanations()).extracting(Explanation::reasonCode)
                .containsExactly("ORDER_DELAYED_MATERIAL_SHORTAGE", "ORDER_PULLED_FORWARD", "DOWNTIME_REDUCED", "ORDER_LATE_DUE");
    }

    @Test
    void kpiReflectsThePlan() {
        assertThat(r.kpi().lateOrders()).isEqualTo(1);
        assertThat(r.kpi().totalLatenessDays()).isEqualTo(4);
        assertThat(r.kpi().movedOperations()).isEqualTo(8);
        assertThat(r.kpi().ordersWithShortage()).isZero();
        assertThat(r.kpi().onTimeRate()).isEqualTo(0.875);
        assertThat(r.objective().downtime()).isEqualTo(160.0);
        assertThat(r.objective().lateness()).isEqualTo(200.0);
    }

    @Test
    void frozenOrdersUntouched() {
        assertThat(r.operations()).filteredOn(o -> o.orderCode().equals("WO-2026-012")).noneMatch(ScheduledOperation::changed);
        assertThat(op("WO-2026-013/10").changed()).isFalse();
    }

    private static OrderResult order(String code) {
        return r.orders().stream().filter(o -> o.orderCode().equals(code)).findFirst().orElseThrow();
    }

    private static ScheduledOperation op(String code) {
        return r.operations().stream().filter(o -> o.operationCode().equals(code)).findFirst().orElseThrow();
    }

    private static Explanation explanation(String reason, String orderCode) {
        return r.explanations().stream().filter(e -> e.reasonCode().equals(reason) && e.orderCode().equals(orderCode))
                .findFirst().orElseThrow(() -> new AssertionError("missing explanation " + reason + " " + orderCode));
    }
}
