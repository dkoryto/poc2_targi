package com.dspc.planning.model;

public record ObjectiveBreakdown(
        double total, double lateness, double shortage, double downtime,
        double deliveryBreach, double change, double changeover) {}
