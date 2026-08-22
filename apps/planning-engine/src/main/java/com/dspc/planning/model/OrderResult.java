package com.dspc.planning.model;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.List;

public record OrderResult(
        String orderCode,
        String lineCode,
        LocalDateTime plannedStart,
        LocalDateTime plannedEnd,
        LocalDate dueDate,
        int latenessDays,
        boolean materialComplete,
        List<Shortage> shortages) {}
