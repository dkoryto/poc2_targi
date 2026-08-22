package com.dspc.planning.solver;

import com.dspc.planning.config.SolverProperties;
import com.dspc.planning.model.PlanningRequest;
import com.dspc.planning.model.PlanningResponse;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;

import java.io.IOException;
import java.io.InputStream;
import java.io.UncheckedIOException;
import java.time.LocalDate;

public final class Fixtures {
    public static final LocalDate T0 = LocalDate.of(2026, 9, 7);
    public static final ObjectMapper MAPPER = new ObjectMapper()
            .registerModule(new JavaTimeModule())
            .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);

    private Fixtures() {}

    public static PlanningRequest load(String name) {
        try (InputStream in = Fixtures.class.getResourceAsStream("/scenarios/" + name + ".json")) {
            if (in == null) throw new IllegalStateException("Missing fixture " + name);
            return MAPPER.readValue(in, PlanningRequest.class);
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
    }

    public static String raw(String name) {
        try (InputStream in = Fixtures.class.getResourceAsStream("/scenarios/" + name + ".json")) {
            return new String(in.readAllBytes());
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
    }

    public static HeuristicSolver solver() { return new HeuristicSolver(new SolverProperties(2500, 10)); }

    public static PlanningResponse solve(String name) { return solver().solve(load(name)); }

    public static LocalDate day(int offset) { return T0.plusDays(offset); }
}
