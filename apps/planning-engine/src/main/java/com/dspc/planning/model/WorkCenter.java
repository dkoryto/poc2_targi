package com.dspc.planning.model;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;

import java.util.List;

public record WorkCenter(
        @NotBlank String code,
        String lineCode,
        Double hoursPerDay,
        Double capacityFactor,
        @Valid List<CalendarDay> calendar) {

    public double hoursPerDayOrDefault() { return hoursPerDay == null ? 16.0 : hoursPerDay; }
    public double capacityFactorOrDefault() { return capacityFactor == null ? 1.0 : capacityFactor; }
}
