# Third-party licences (commercial use compatible)

| Component | Licence | Where |
|---|---|---|
| ASP.NET Core, EF Core, SignalR, Npgsql | MIT / PostgreSQL licence | business-api |
| Serilog, FluentValidation, Swashbuckle | Apache-2.0 | business-api |
| QuestPDF | **Community licence** (free for organisations < 1 M USD annual revenue; otherwise commercial licence required — verify before commercial deployment) | business-api (PDF) |
| QRCoder | MIT | business-api |
| SkiaSharp.NativeAssets.Linux | MIT | business-api (QuestPDF rendering backend in the container) |
| AWSSDK.S3 | Apache-2.0 | business-api (MinIO client) |
| Testcontainers, xUnit, FluentAssertions | MIT / Apache-2.0 | tests |
| Spring Boot, Jackson | Apache-2.0 | planning-engine |
| React, Vite, TanStack Query, react-router, react-hook-form, zod, i18next, date-fns, lucide-react, @microsoft/signalr, MSW, Vitest, Playwright | MIT / BSD / Apache-2.0 | web, e2e |
| MapLibre GL JS | BSD-3-Clause | web |
| Natural Earth (europe.geojson) | Public domain | web |
| PostgreSQL | PostgreSQL licence | infra |
| MinIO | AGPL-3.0 (used as an unmodified separate service over its S3 API — no linking; for production consider a licensed S3 store) | infra |
