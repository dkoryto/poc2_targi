package com.dspc.planning.model;

import jakarta.validation.Valid;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;

import java.time.LocalDate;
import java.util.List;

public record PlanOrder(
        @NotBlank String code,
        String productCode,
        @Min(1) @Max(5) int priority,
        int quantity,
        @NotNull LocalDate dueDate,
        @NotNull LocalDate releaseDate,
        Boolean frozen,
        String lineCode,
        @NotEmpty @Valid List<PlanOperation> operations) {

    public boolean isFrozen() { return Boolean.TRUE.equals(frozen); }
    public String productCodeOrEmpty() { return productCode == null ? "" : productCode; }
}
