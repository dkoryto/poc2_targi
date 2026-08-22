package com.dspc.planning.api;

import com.dspc.planning.model.PlanningRequest;
import com.dspc.planning.model.PlanningResponse;
import com.dspc.planning.solver.HeuristicSolver;
import jakarta.validation.Valid;
import org.springframework.http.MediaType;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping(path = "/api/v1/plan", produces = MediaType.APPLICATION_JSON_VALUE)
public class PlanController {
    private final HeuristicSolver solver;

    public PlanController(HeuristicSolver solver) { this.solver = solver; }

    @PostMapping(path = "/solve", consumes = MediaType.APPLICATION_JSON_VALUE)
    public PlanningResponse solve(@Valid @RequestBody PlanningRequest request) {
        return solver.solve(request);
    }
}
