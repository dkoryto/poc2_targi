# ADR-0002 — Local JWT identity provider (no Keycloak in v1)

**Status:** accepted · **Date:** 2026-08-23

## Context
Spec allows either an OIDC provider started from Compose or a "secure local demo variant". Keycloak adds ~1 GB RAM, slow start and realm import complexity to a stand demo.

## Decision
`business-api` issues HS256 JWTs itself (`JwtTokenIssuer`, PBKDF2-SHA256 password hashes, key from `Identity__Jwt__Key`, fail-fast if missing outside Development). Roles and supplier scope travel as claims (`role`, `supplier_id`, `supplier_code`, `site_id`). Validation uses standard `JwtBearer`, so switching to an external OIDC provider is a configuration change (authority/audience + claim mapping), not a code change. Demo profile adds `GET /auth/demo-login?role=` and `GET /auth/demo-accounts`; both return 404 when `Demo__Enabled=false`.

## Consequences
- No MFA, no password policy, no token revocation — documented in SECURITY.md as pre-production gaps.
- RBAC is enforced by authorization policies on every endpoint group and by `ISupplierScope` inside queries, so UI hiding is never the only barrier.
