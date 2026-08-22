package com.dspc.planning.model;

import java.time.LocalDate;

/** Quantity of a part not covered by free on-hand stock; availableOn = inbound ETA that covers it, null = not covered inside horizon. */
public record Shortage(String partCode, double quantity, LocalDate availableOn) {}
