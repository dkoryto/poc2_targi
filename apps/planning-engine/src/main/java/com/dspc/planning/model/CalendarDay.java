package com.dspc.planning.model;

import jakarta.validation.constraints.NotNull;

import java.time.LocalDate;

public record CalendarDay(@NotNull LocalDate date, double availableHours) {}
