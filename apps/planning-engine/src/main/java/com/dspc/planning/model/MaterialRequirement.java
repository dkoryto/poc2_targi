package com.dspc.planning.model;

import jakarta.validation.constraints.NotBlank;

public record MaterialRequirement(@NotBlank String partCode, double quantity) {}
