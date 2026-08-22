# SECURITY.md — Defense Supply & Production Control (demonstrator)

> Demonstrator wykorzystuje fikcyjne dane. Prezentowane mapowanie wymagań jakościowych nie stanowi formalnego potwierdzenia zgodności ani certyfikacji. The system is **not** certified, **not** assessed against NATO/AQAP/STANAG, and must not process classified information.

## Scope and boundaries

- Runs fully offline on a single machine via Docker Compose; all ports bind to `127.0.0.1`.
- Single tenant, single site, fictional suppliers, no real contract, location or weapon-system data.
- Demo profile (`Demo__Enabled=true`) deliberately weakens authentication (`/api/v1/auth/demo-login`, role switcher, `/api/v1/demo/reset`). **Never expose the demo profile on a network.** With `Demo__Enabled=false` those endpoints return 404 and full username/password + JWT + RBAC applies.

## Threat model (STRIDE, condensed)

| Threat | Control in demonstrator | Gap before production |
|---|---|---|
| Spoofing | Local JWT (HS256, key from env, fail-fast if missing outside Development); passwords hashed (PBKDF2/Argon2 per implementation); rate-limited login | Swap to OIDC/OAuth2 provider (Keycloak/Entra), MFA, hardware-key for QualityInspector approvals, short-lived tokens + refresh, key rotation |
| Tampering | Optimistic concurrency (`If-Match`), idempotency keys, append-only audit table (DB-level REVOKE/trigger), SHA-256 on every document and passport version | Signed audit chain / WORM storage, signed PDFs (PAdES), TLS everywhere (Compose is plain HTTP on loopback) |
| Repudiation | Audit event per mutation: who, when, what, before/after, correlation id, source | Central SIEM forwarding, clock sync |
| Information disclosure | Supplier scope enforced in every query (`ISupplierScope`), object storage reachable only through API, redaction of secrets in logs, security headers, CORS allow-list | Field-level classification, encryption at rest for Postgres/MinIO, DLP on uploads, secrets manager instead of `.env` |
| Denial of service | Rate limits (login, upload, scenario run), solver time limit + deterministic fallback, upload size cap 10 MB | WAF, resource quotas, HA Postgres |
| Elevation of privilege | Role policies in API (not just UI), demo role switcher only in demo profile, Administrator cannot edit audit | Separation of duties review, privileged access management, periodic access recertification |

## Upload handling

Allowed: `pdf`, `png`, `jpg/jpeg`; MIME sniffed server-side, extension + size checked, file name normalised (no path separators, no control chars, stored under a GUID key), scanned through `IFileScanner` (demo: `NoOpFileScanner` that only logs — replace with ClamAV/ICAP adapter before production), served back only via the API with `Content-Disposition: attachment`.

## Known risks before any production use

1. HTTP without TLS inside Compose; add a TLS-terminating reverse proxy.
2. Demo credentials and JWT key in `.env.example` — regenerate, move to a secrets store.
3. `NoOpFileScanner` — no real malware scanning.
4. Local identity provider — no MFA, no password policy enforcement beyond length.
5. Optional local LLM adapter: outputs are proposals only, but prompt-injection via uploaded documents is possible; keep `LocalAi__Enabled=false` unless the model host is isolated.
6. No backups / DR for Postgres and MinIO volumes.
7. Dependency licences and CVEs: run `dotnet list package --vulnerable`, `mvn dependency-check`, `pnpm audit` before release.

## Reporting

This is a demonstrator; report issues to the project maintainers at dev@silevis.com.
