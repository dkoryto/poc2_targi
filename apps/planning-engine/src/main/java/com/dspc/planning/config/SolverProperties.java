package com.dspc.planning.config;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "dspc.solver")
public record SolverProperties(Integer defaultTimeLimitMs, Integer minOptimiserBudgetMs) {
    public int defaultTimeLimit() { return defaultTimeLimitMs == null ? 2500 : defaultTimeLimitMs; }
    public int minOptimiserBudget() { return minOptimiserBudgetMs == null ? 10 : minOptimiserBudgetMs; }
}
