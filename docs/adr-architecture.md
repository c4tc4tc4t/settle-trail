# ADR: Modular monolith organized by feature

**Status:** Superseded by [Clean Architecture ADR](adr-clean-architecture.md)

## Context

The initial API placed endpoint handlers in `Program.cs`, account operations in `PaymentService`, and persistence types in one file. Upcoming ledger, callback, and reconciliation work will add more behavior to these areas.

## Decision

- Keep one deployable ASP.NET Core project and one SQLite database.
- Organize code by business feature: `Accounts`, `Payments`, and `Ledger`. Each feature owns its endpoints, request and response contracts, validation, and service code as needed.
- Keep HTTP-wide error and health contracts in `Shared`.
- Keep the EF Core context and migrations in `Infrastructure/Persistence`. Feature services may use the context directly while the application is small; introduce an abstraction only when a concrete need appears.
- Keep `Program.cs` limited to service registration, middleware, migration and seed startup, and endpoint mapping.
- Route ledger entry creation through `LedgerPosting`. Future ledger invariants belong in this module.

## Consequences

The public routes, JSON contracts, status codes, and database tables remain the same. The refactor changes CLR type names recorded in the EF model snapshot, so the snapshot and migration designer were updated with the moved entity namespaces. The historical migration ID and table definitions are unchanged.
