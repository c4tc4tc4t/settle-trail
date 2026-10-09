# ADR 002: Public API contracts

**Status:** Accepted

## Context

The demo API originally had inline request and response records, mixed error formats, and validation split between endpoints and the payment service. A caller could receive an empty 400 or 404, a custom JSON error, or Problem Details for similar failures.

## Decisions

- Keep the existing paths and successful status codes, including `201` for account creation and `200` for deposits and transfers, so the documented demo requests continue to work.
- Put named request and response records in `Contracts`. Accept nullable request fields at the JSON boundary so missing fields can produce a useful 400 response.
- Validate non-empty GUIDs, distinct transfer accounts, positive whole minor-unit amounts, names, and idempotency keys before calling the service. Preserve the service checks as defense in depth.
- Trim account names before storing them. Do not normalize idempotency keys: exact reuse is meaningful and keys remain case sensitive.
- Use Problem Details for all documented 400, 404, and 409 responses. The `code` extension is the stable machine-readable identifier; `detail` is explanatory text and may change.
- Keep payment and account absence under one `resource_not_found` code. Separate idempotency conflicts from insufficient funds with distinct 409 codes.
- List success and error responses in OpenAPI endpoint metadata. Document field constraints and error codes in the README.

## Consequences

Clients can branch on HTTP status and `code` without parsing prose. The boundary now rejects omitted fields and malformed IDs, while the payment service retains its business safeguards. Future endpoints should reuse the same error format and add new stable codes only for distinct client actions.
