package com.dspc.planning.model;

public record PlanKpi(
        double downtimeHours,
        int lateOrders,
        int totalLatenessDays,
        int movedOperations,
        int ordersWithShortage,
        double onTimeRate) {}
