package com.dspc.planning.solver;

import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

/** Occupancy of one work center: one operation at a time, half-open intervals [start, end). */
final class Timeline {
    record Slot(LocalDateTime start, LocalDateTime end, String operationCode, String productCode) {}

    private static final Comparator<Slot> ORDER = Comparator.comparing(Slot::start).thenComparing(Slot::operationCode);
    private final List<Slot> slots = new ArrayList<>();

    void add(Slot s) {
        int idx = Collections.binarySearch(slots, s, ORDER);
        if (idx < 0) idx = -idx - 1;
        slots.add(idx, s);
    }

    Slot firstConflict(LocalDateTime start, LocalDateTime end) {
        for (Slot s : slots) {
            if (s.start().isBefore(end) && start.isBefore(s.end())) return s;
            if (!s.start().isBefore(end)) break;
        }
        return null;
    }

    LocalDateTime findStart(LocalDateTime earliest, long minutes, WorkCalendar cal) {
        LocalDateTime s = cal.next(earliest);
        for (int guard = 0; guard < 100_000; guard++) {
            LocalDateTime e = cal.add(s, minutes);
            Slot c = firstConflict(s, e);
            if (c == null) return s;
            s = cal.next(c.end());
        }
        throw new IllegalStateException("Could not find a free slot after " + earliest);
    }

    List<Slot> slots() { return Collections.unmodifiableList(slots); }
}
