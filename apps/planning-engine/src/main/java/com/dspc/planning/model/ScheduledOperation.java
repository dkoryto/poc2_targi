package com.dspc.planning.model;

import java.time.LocalDateTime;

public record ScheduledOperation(
        String orderCode,
        String operationCode,
        String workCenterCode,
        String lineCode,
        LocalDateTime start,
        LocalDateTime end,
        boolean changed,
        double shiftDays,
        boolean waitingForMaterial) {}
