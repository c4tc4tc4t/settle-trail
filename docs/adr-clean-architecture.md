# ADR: Clean Architecture with CQRS

**Status:** Accepted

## Context

The single-project modular layout kept handlers, EF Core, and domain types in one assembly. The next payment stories will add more rules and integrations, so project boundaries are useful now.

## Decision

- `SettleTrail.Domain` owns the account, payment, and ledger models and balanced ledger posting. It has no package or project dependencies.
- `SettleTrail.Application` owns commands, queries, handlers, FluentValidation rules, and persistence interfaces. It depends only on Domain and FluentValidation.
- `SettleTrail.Infrastructure` implements repositories and unit of work with EF Core and owns the SQLite context and migrations. It depends on Application and Domain.
- `SettleTrail.Api` owns HTTP contracts, Problem Details, OpenAPI, and the composition root. Endpoints dispatch commands and queries through Wolverine's in-process `IMessageBus`.
- The API explicitly runs FluentValidation command validators before dispatching through Wolverine. This follows the Minimal API integration path and maps failures to the existing stable Problem Details codes.
- Wolverine is used locally for CQRS in this story. Durable messaging, outbox, and external transport remain in their planned stories.
- Wolverine 6 uses runtime code generation here. The EF Core context is explicitly allowed as a service-location dependency because its scoped options are registered through an opaque factory. Other repository registrations remain constructor-based.

## Consequences

The dependency direction is `Api -> Application <- Infrastructure` and `Application -> Domain`. EF Core's `DbContext` remains the unit of work implementation; repositories express the queries and writes needed by use cases. Public routes, success responses, and database table definitions remain unchanged. Existing migration IDs were preserved while moving migrations to the Infrastructure assembly.
