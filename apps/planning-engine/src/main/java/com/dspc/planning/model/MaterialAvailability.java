package com.dspc.planning.model;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;

import java.util.List;

public record MaterialAvailability(
        @NotBlank String partCode,
        double onHand,
        double reserved,
        @Valid List<InboundLot> inbound) {
    public List<InboundLot> inboundOrEmpty() { return inbound == null ? List.of() : inbound; }
}
