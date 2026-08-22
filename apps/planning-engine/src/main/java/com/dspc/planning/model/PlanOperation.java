package com.dspc.planning.model;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Positive;

import java.time.LocalDateTime;
import java.util.List;

public record PlanOperation(
        @NotBlank String code,
        int sequence,
        @NotBlank String workCenterCode,
        @Positive double durationHours,
        Boolean frozen,
        LocalDateTime baselineStart,
        LocalDateTime baselineEnd,
        @Valid List<MaterialRequirement> materialRequirements) {

    public boolean isFrozen() { return Boolean.TRUE.equals(frozen); }
    public List<MaterialRequirement> requirements() { return materialRequirements == null ? List.of() : materialRequirements; }
    public boolean hasBaseline() { return baselineStart != null && baselineEnd != null; }
}
