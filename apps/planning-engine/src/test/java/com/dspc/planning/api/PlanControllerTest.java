package com.dspc.planning.api;

import com.dspc.planning.solver.Fixtures;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

import static org.hamcrest.Matchers.hasItem;
import static org.hamcrest.Matchers.lessThan;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@SpringBootTest
@AutoConfigureMockMvc
class PlanControllerTest {
    @Autowired MockMvc mvc;

    @Test
    void solvesAct40ScenarioOverHttp() throws Exception {
        mvc.perform(post("/api/v1/plan/solve").contentType(MediaType.APPLICATION_JSON).content(Fixtures.raw("act40-delay")))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.status").value("FEASIBLE"))
                .andExpect(jsonPath("$.solver").value("dspc-heuristic/1.0"))
                .andExpect(jsonPath("$.elapsedMs").value(lessThan(3000)))
                .andExpect(jsonPath("$.kpi.downtimeHours").value(8.0))
                .andExpect(jsonPath("$.operations[?(@.operationCode=='WO-2026-019/20')].start").value(hasItem("2026-09-16T14:00:00")))
                .andExpect(jsonPath("$.orders[?(@.orderCode=='WO-2026-014')].latenessDays").value(hasItem(4)))
                .andExpect(jsonPath("$.explanations[?(@.reasonCode=='ORDER_PULLED_FORWARD')].orderCode").value(hasItem("WO-2026-019")));
    }

    @Test
    void rejectsInvalidRequestWithProblemDetails() throws Exception {
        mvc.perform(post("/api/v1/plan/solve").contentType(MediaType.APPLICATION_JSON)
                        .content("{\"scenarioId\":\"\",\"horizonStart\":\"2026-09-07\",\"horizonEnd\":\"2026-11-30\",\"workCenters\":[],\"orders\":[],\"materials\":[]}"))
                .andExpect(status().isBadRequest())
                .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON))
                .andExpect(jsonPath("$.title").value("Invalid planning request"))
                .andExpect(jsonPath("$.errors.scenarioId").exists());
    }

    @Test
    void rejectsFrozenOperationWithoutBaseline() throws Exception {
        String body = "{\"scenarioId\":\"X\",\"horizonStart\":\"2026-09-07\",\"horizonEnd\":\"2026-11-30\"," +
                "\"workCenters\":[{\"code\":\"WC-X\"}],\"materials\":[]," +
                "\"orders\":[{\"code\":\"WO-1\",\"priority\":3,\"quantity\":1,\"dueDate\":\"2026-09-20\",\"releaseDate\":\"2026-09-07\"," +
                "\"operations\":[{\"code\":\"WO-1/10\",\"sequence\":10,\"workCenterCode\":\"WC-X\",\"durationHours\":8,\"frozen\":true}]}]}";
        mvc.perform(post("/api/v1/plan/solve").contentType(MediaType.APPLICATION_JSON).content(body))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.detail").value(org.hamcrest.Matchers.containsString("baselineStart")));
    }

    @Test
    void servesContractAndHealth() throws Exception {
        mvc.perform(get("/v3/api-docs")).andExpect(status().isOk())
                .andExpect(content().string(org.hamcrest.Matchers.containsString("DSPC Planning Engine")));
        mvc.perform(get("/actuator/health")).andExpect(status().isOk()).andExpect(jsonPath("$.status").value("UP"));
    }
}
