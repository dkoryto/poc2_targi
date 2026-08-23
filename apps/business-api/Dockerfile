# Build context = repository root (needs packages/demo-data). See docker-compose.yml.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY apps/business-api/Directory.Build.props apps/business-api/Directory.Packages.props apps/business-api/Dspc.slnx ./
COPY apps/business-api/src/Dspc.Domain/Dspc.Domain.csproj src/Dspc.Domain/
COPY apps/business-api/src/Dspc.Application/Dspc.Application.csproj src/Dspc.Application/
COPY apps/business-api/src/Dspc.Infrastructure/Dspc.Infrastructure.csproj src/Dspc.Infrastructure/
COPY apps/business-api/src/Dspc.Api/Dspc.Api.csproj src/Dspc.Api/
RUN dotnet restore src/Dspc.Api/Dspc.Api.csproj
COPY apps/business-api/src ./src
RUN dotnet publish src/Dspc.Api/Dspc.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates tzdata && rm -rf /var/lib/apt/lists/* \
    && useradd -r -u 10001 -g root dspc && mkdir -p /data/storage /app/demo-data && chown -R dspc:root /data /app
COPY --from=build /app/publish ./
COPY packages/demo-data /app/demo-data
ENV ASPNETCORE_URLS=http://+:5080 \
    ASPNETCORE_ENVIRONMENT=Demo \
    Seed__Path=/app/demo-data \
    Storage__Root=/data/storage \
    TZ=Europe/Warsaw \
    DOTNET_gcServer=0
USER dspc
EXPOSE 5080
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=10 CMD curl -fs http://localhost:5080/health/live || exit 1
ENTRYPOINT ["dotnet", "Dspc.Api.dll"]
