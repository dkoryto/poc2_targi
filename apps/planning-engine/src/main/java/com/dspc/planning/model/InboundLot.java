package com.dspc.planning.model;

import jakarta.validation.constraints.NotNull;

import java.time.LocalDate;

public record InboundLot(double quantity, @NotNull LocalDate eta, String reference, Integer riskScore) {}
