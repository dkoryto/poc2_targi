package com.dspc.planning.api;

import org.springframework.core.io.ClassPathResource;
import org.springframework.core.io.Resource;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

/** Serves the hand-maintained contract (packages/contracts/planning-engine.yaml) verbatim. */
@RestController
public class OpenApiController {
    @GetMapping(path = {"/v3/api-docs", "/openapi.yaml"}, produces = "application/yaml")
    public ResponseEntity<Resource> contract() {
        return ResponseEntity.ok().contentType(MediaType.parseMediaType("application/yaml"))
                .body(new ClassPathResource("static/openapi.yaml"));
    }
}
