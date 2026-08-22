package com.dspc.planning.solver;

import com.dspc.planning.model.CalendarDay;
import org.junit.jupiter.api.Test;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class WorkCalendarTest {
    private final WorkCalendar cal = new WorkCalendar(16, 1.0, List.of());

    @Test
    void spansWeekend() {
        LocalDateTime fri = LocalDateTime.of(2026, 9, 25, 6, 0);
        assertThat(cal.add(fri, 36 * 60)).isEqualTo(LocalDateTime.of(2026, 9, 29, 10, 0));
        assertThat(cal.subtract(LocalDateTime.of(2026, 9, 29, 10, 0), 36 * 60)).isEqualTo(fri);
        assertThat(cal.between(fri, LocalDateTime.of(2026, 9, 29, 10, 0))).isEqualTo(36 * 60);
    }

    @Test
    void nextSkipsNightsAndWeekends() {
        assertThat(cal.next(LocalDateTime.of(2026, 9, 11, 22, 0))).isEqualTo(LocalDateTime.of(2026, 9, 14, 6, 0));
        assertThat(cal.next(LocalDateTime.of(2026, 9, 12, 10, 0))).isEqualTo(LocalDateTime.of(2026, 9, 14, 6, 0));
        assertThat(cal.next(LocalDateTime.of(2026, 9, 14, 3, 0))).isEqualTo(LocalDateTime.of(2026, 9, 14, 6, 0));
        assertThat(cal.next(LocalDateTime.of(2026, 9, 14, 9, 0))).isEqualTo(LocalDateTime.of(2026, 9, 14, 9, 0));
    }

    @Test
    void capacityFactorAndOverrides() {
        WorkCalendar half = new WorkCalendar(16, 0.5, List.of(new CalendarDay(LocalDate.of(2026, 9, 8), 0)));
        assertThat(half.minutesOn(LocalDate.of(2026, 9, 7))).isEqualTo(8 * 60);
        assertThat(half.minutesOn(LocalDate.of(2026, 9, 8))).isZero();
        assertThat(half.add(LocalDateTime.of(2026, 9, 7, 6, 0), 12 * 60)).isEqualTo(LocalDateTime.of(2026, 9, 9, 10, 0));
    }
}
