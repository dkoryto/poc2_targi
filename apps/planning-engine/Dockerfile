# syntax=docker/dockerfile:1
FROM maven:3.9-eclipse-temurin-17 AS build
WORKDIR /build
COPY pom.xml .
RUN --mount=type=cache,target=/root/.m2 mvn -q -B dependency:go-offline
COPY src ./src
RUN --mount=type=cache,target=/root/.m2 mvn -q -B -DskipTests package && cp target/planning-engine-*.jar app.jar

FROM eclipse-temurin:17-jre
RUN groupadd --system dspc && useradd --system --gid dspc --no-create-home dspc
WORKDIR /app
COPY --from=build /build/app.jar app.jar
USER dspc
EXPOSE 8081
ENV JAVA_OPTS="-XX:+UseSerialGC -XX:TieredStopAtLevel=1 -Xss512k -XX:MaxRAMPercentage=60"
# no curl/wget in the JRE image: plain bash TCP probe keeps the runtime image minimal
HEALTHCHECK --interval=10s --timeout=3s --start-period=25s --retries=6 \
  CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8081; printf "GET /actuator/health HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3; grep -q "\"UP\"" <&3' || exit 1
ENTRYPOINT ["sh", "-c", "exec java $JAVA_OPTS -jar app.jar"]
