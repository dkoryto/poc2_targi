package com.dspc.planning.solver;

import com.dspc.planning.model.InboundLot;
import com.dspc.planning.model.MaterialAvailability;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Cumulative, order-of-arrival material allocation per part. Free stock = onHand − reserved, inbound adds at ETA 06:00. */
final class MaterialLedger {
    private static final double EPS = 1e-9;

    record Allocation(String partCode, double quantity, LocalDateTime availableAt, String reference,
                      double missingFromStock, boolean coveredInHorizon, LocalDate coveringEta) {
        boolean onHand() { return missingFromStock <= 0; }
    }

    private static final class Part {
        final double base;
        final List<InboundLot> inbound;
        double allocated;
        Part(double base, List<InboundLot> inbound) { this.base = base; this.inbound = inbound; }
    }

    private final Map<String, Part> parts = new LinkedHashMap<>();
    private final LocalDate horizonEnd;

    MaterialLedger(List<MaterialAvailability> materials, LocalDate horizonEnd) {
        this.horizonEnd = horizonEnd;
        for (MaterialAvailability m : materials) {
            List<InboundLot> lots = new ArrayList<>(m.inboundOrEmpty());
            lots.sort(Comparator.comparing(InboundLot::eta)
                    .thenComparing(l -> l.reference() == null ? "" : l.reference())
                    .thenComparingDouble(InboundLot::quantity));
            parts.put(m.partCode(), new Part(Math.max(0, m.onHand() - m.reserved()), lots));
        }
    }

    boolean knows(String partCode) { return parts.containsKey(partCode); }

    /** Unknown parts are treated as unconstrained (on hand). */
    Allocation allocate(String partCode, double qty) {
        Part p = parts.get(partCode);
        if (p == null) return new Allocation(partCode, qty, null, null, 0, true, null);
        p.allocated += qty;
        double need = p.allocated;
        if (need <= p.base + EPS) return new Allocation(partCode, qty, null, null, 0, true, null);
        double missing = Math.min(qty, need - p.base);
        double cum = p.base;
        for (InboundLot lot : p.inbound) {
            cum += lot.quantity();
            if (cum + EPS >= need) {
                boolean inside = !lot.eta().isAfter(horizonEnd);
                return new Allocation(partCode, qty, lot.eta().atTime(WorkCalendar.DAY_START), lot.reference(),
                        missing, inside, lot.eta());
            }
        }
        return new Allocation(partCode, qty, null, null, missing, false, null);
    }
}
