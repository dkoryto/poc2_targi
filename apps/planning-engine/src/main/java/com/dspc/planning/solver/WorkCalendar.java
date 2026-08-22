package com.dspc.planning.solver;

import com.dspc.planning.model.CalendarDay;

import java.time.DayOfWeek;
import java.time.Duration;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Working-time arithmetic for one work center. Working window starts at 06:00 site-local and lasts
 * {@code hoursPerDay × capacityFactor} on Mon–Fri (0 h on weekends) unless a calendar override exists for the date.
 */
public final class WorkCalendar {
    public static final LocalTime DAY_START = LocalTime.of(6, 0);
    private static final int MAX_SCAN_DAYS = 5000;

    private final long defaultMinutes;
    private final Map<LocalDate, Long> overrides = new HashMap<>();

    public WorkCalendar(double hoursPerDay, double capacityFactor, List<CalendarDay> calendar) {
        this.defaultMinutes = Math.max(0, Math.round(hoursPerDay * capacityFactor * 60));
        if (calendar != null) {
            for (CalendarDay d : calendar) {
                overrides.put(d.date(), Math.max(0, Math.round(d.availableHours() * capacityFactor * 60)));
            }
        }
    }

    public long minutesOn(LocalDate d) {
        Long o = overrides.get(d);
        if (o != null) return o;
        DayOfWeek w = d.getDayOfWeek();
        return (w == DayOfWeek.SATURDAY || w == DayOfWeek.SUNDAY) ? 0 : defaultMinutes;
    }

    public LocalDateTime windowStart(LocalDate d) { return d.atTime(DAY_START); }

    public LocalDateTime windowEnd(LocalDate d) { return windowStart(d).plusMinutes(minutesOn(d)); }

    /** Earliest working instant ≥ t. */
    public LocalDateTime next(LocalDateTime t) {
        LocalDateTime cur = t;
        for (int i = 0; i < MAX_SCAN_DAYS; i++) {
            LocalDate d = cur.toLocalDate();
            if (minutesOn(d) > 0) {
                LocalDateTime ws = windowStart(d), we = windowEnd(d);
                if (!cur.isAfter(ws)) return ws;
                if (cur.isBefore(we)) return cur;
            }
            cur = d.plusDays(1).atTime(DAY_START);
        }
        throw new IllegalStateException("No working time found after " + t);
    }

    /** Latest working instant ≤ t. */
    public LocalDateTime prev(LocalDateTime t) {
        LocalDateTime cur = t;
        for (int i = 0; i < MAX_SCAN_DAYS; i++) {
            LocalDate d = cur.toLocalDate();
            if (minutesOn(d) > 0) {
                LocalDateTime ws = windowStart(d), we = windowEnd(d);
                if (!cur.isBefore(we)) return we;
                if (cur.isAfter(ws)) return cur;
            }
            cur = d.minusDays(1).atTime(LocalTime.of(23, 59));
        }
        throw new IllegalStateException("No working time found before " + t);
    }

    /** Instant reached after {@code minutes} of working time starting at (or after) {@code from}. */
    public LocalDateTime add(LocalDateTime from, long minutes) {
        LocalDateTime cur = next(from);
        long rem = minutes;
        for (int i = 0; i < MAX_SCAN_DAYS; i++) {
            LocalDate d = cur.toLocalDate();
            long avail = Duration.between(cur, windowEnd(d)).toMinutes();
            if (rem <= avail) return cur.plusMinutes(rem);
            rem -= avail;
            cur = next(d.plusDays(1).atTime(DAY_START));
        }
        throw new IllegalStateException("Cannot add " + minutes + " working minutes to " + from);
    }

    /** Instant such that {@code minutes} of working time fit between it and {@code to}. */
    public LocalDateTime subtract(LocalDateTime to, long minutes) {
        LocalDateTime cur = prev(to);
        long rem = minutes;
        for (int i = 0; i < MAX_SCAN_DAYS; i++) {
            LocalDate d = cur.toLocalDate();
            long avail = Duration.between(windowStart(d), cur).toMinutes();
            if (rem <= avail) return cur.minusMinutes(rem);
            rem -= avail;
            cur = prev(d.minusDays(1).atTime(LocalTime.of(23, 59)));
        }
        throw new IllegalStateException("Cannot subtract " + minutes + " working minutes from " + to);
    }

    /** Working minutes inside [a, b). */
    public long between(LocalDateTime a, LocalDateTime b) {
        if (!b.isAfter(a)) return 0;
        long total = 0;
        LocalDate d = a.toLocalDate();
        LocalDate last = b.toLocalDate();
        while (!d.isAfter(last)) {
            if (minutesOn(d) > 0) {
                LocalDateTime ws = windowStart(d), we = windowEnd(d);
                LocalDateTime s = a.isAfter(ws) ? a : ws;
                LocalDateTime e = b.isBefore(we) ? b : we;
                if (e.isAfter(s)) total += Duration.between(s, e).toMinutes();
            }
            d = d.plusDays(1);
        }
        return total;
    }
}
