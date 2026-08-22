package com.dspc.planning.model;

import java.util.Map;

public record Explanation(String reasonCode, String orderCode, Map<String, Object> params) {}
