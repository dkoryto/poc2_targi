Identity is provided locally by business-api (`Identity:LocalProvider`, JWT HS256) — see `docs/adr/0002-local-jwt-identity.md`.
To plug an external OIDC provider (e.g. Keycloak) add a service here, set `Identity__Provider=Oidc`, `Identity__Oidc__Authority`, `Identity__Oidc__Audience`, and map realm roles to the DSPC roles (`SupplierUser` needs a `supplier_code` claim).
