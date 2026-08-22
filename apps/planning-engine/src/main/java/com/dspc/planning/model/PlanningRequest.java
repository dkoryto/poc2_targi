package com.dspc.planning.model;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

import java.time.LocalDate;
import java.util.List;

public record PlanningRequest(
        @NotBlank String scenarioId,
        String baselineId,
        @NotNull LocalDate horizonStart,
        @NotNull LocalDate horizonEnd,
        Integer timeLimitMs,
        @NotNull @Valid List<WorkCenter> workCenters,
        @NotNull @Valid List<PlanOrder> orders,
        @NotNull @Valid List<MaterialAvailability> materials,
        @Valid ObjectiveWeights weights) {

    public ObjectiveWeights weightsOrDefault() {
        return weights == null ? ObjectiveWeights.defaults() : weights;
    }
}
