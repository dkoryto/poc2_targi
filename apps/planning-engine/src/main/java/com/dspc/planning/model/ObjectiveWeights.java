package com.dspc.planning.model;

public record ObjectiveWeights(
        Double latenessPerDayPerPriority,
        Double shortagePerUnit,
        Double downtimePerHour,
        Double deliveryBreachPerOrder,
        Double changePerMovedOperation,
        Double changeoverPerSwitch) {

    public static ObjectiveWeights defaults() { return new ObjectiveWeights(10.0, 5.0, 20.0, 100.0, 2.0, 8.0); }

    public double lateness() { return latenessPerDayPerPriority == null ? 10.0 : latenessPerDayPerPriority; }
    public double shortage() { return shortagePerUnit == null ? 5.0 : shortagePerUnit; }
    public double downtime() { return downtimePerHour == null ? 20.0 : downtimePerHour; }
    public double deliveryBreach() { return deliveryBreachPerOrder == null ? 100.0 : deliveryBreachPerOrder; }
    public double change() { return changePerMovedOperation == null ? 2.0 : changePerMovedOperation; }
    public double changeover() { return changeoverPerSwitch == null ? 8.0 : changeoverPerSwitch; }
}
