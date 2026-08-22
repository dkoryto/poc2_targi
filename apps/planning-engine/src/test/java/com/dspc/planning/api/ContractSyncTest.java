package com.dspc.planning.api;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledIf;

import java.nio.file.Files;
import java.nio.file.Path;

import static org.assertj.core.api.Assertions.assertThat;

/** The bundled contract must equal packages/contracts/planning-engine.yaml (skipped outside the monorepo). */
class ContractSyncTest {
    static final Path SOURCE = Path.of("..", "..", "packages", "contracts", "planning-engine.yaml");

    static boolean monorepo() { return Files.exists(SOURCE); }

    @Test
    @EnabledIf("monorepo")
    void bundledContractMatchesMonorepoContract() throws Exception {
        String bundled = Files.readString(Path.of("src", "main", "resources", "static", "openapi.yaml"));
        assertThat(bundled).isEqualTo(Files.readString(SOURCE));
    }
}
