package com.dspc.planning;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;

@SpringBootApplication
@org.springframework.boot.context.properties.ConfigurationPropertiesScan
public class PlanningEngineApplication {
    public static void main(String[] args) {
        SpringApplication.run(PlanningEngineApplication.class, args);
    }
}
