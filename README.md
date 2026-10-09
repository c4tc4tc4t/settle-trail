# SettleTrail

SettleTrail is a learning project that simulates payment posting with a balanced ledger. The first backend version supports demo accounts, deposits, transfers, and idempotent requests. It does not move real money.

## Stack

- .NET 10 and ASP.NET Core Minimal API
- EF Core with SQLite
- xUnit tests
- English names, responses, and documentation

## Run locally

Install the .NET 10 SDK. From a fresh clone, enter the repository directory and run:

    dotnet tool restore
    dotnet restore SettleTrail.slnx
    dotnet build SettleTrail.slnx --no-restore
    dotnet test SettleTrail.slnx --no-build
    dotnet run --project src/SettleTrail.Api --launch-profile http

The API listens at http://localhost:5193 with this launch profile. Check it at http://localhost:5193/health. Stop it with Ctrl+C.

The API creates a local SQLite file named `settletrail.db` in `src/SettleTrail.Api` and applies EF Core migrations on startup. The file is ignored by Git. Set the `ConnectionStrings__SettleTrail` environment variable to use another connection string.

In Development, the OpenAPI document is available at http://localhost:5193/openapi/v1.json.

Request and error contract decisions are recorded in [ADR 002](docs/adr-002-api-contracts.md). The [glossary](docs/glossary.md) defines the public payment terms.

## Architecture

The backend is a single deployable application with four projects. `SettleTrail.Domain` owns business models and ledger posting. `SettleTrail.Application` owns CQRS commands, queries, Wolverine handlers, FluentValidation rules, and repository interfaces. `SettleTrail.Infrastructure` implements the repositories and unit of work with EF Core and SQLite. `SettleTrail.Api` owns HTTP contracts, OpenAPI, Problem Details, and dependency injection. Wolverine dispatches commands and queries in process; durable messaging and outbox processing are planned for later stories. See the [Clean Architecture decision](docs/adr-clean-architecture.md).

For later checks, run from the repository root:

    dotnet build SettleTrail.slnx
    dotnet test SettleTrail.slnx --no-build

GitHub Actions runs the same tool restore, package restore, build, and test commands on pushes and pull requests.

## API

Money amounts use **minor units** to avoid floating-point rounding. For BRL, 2500 means R$ 25.00. This version uses BRL only.

| Method | Path | Purpose |
| --- | --- | --- |
| GET | /health | Check if the API is running |
| POST | /accounts | Create a demo account |
| GET | /accounts/{id} | Get an account and its balance |
| POST | /accounts/{id}/deposits | Add demo funds to an account |
| POST | /payments | Transfer between demo accounts |
| GET | /payments/{id} | Get a payment |

Create an account:

    POST /accounts
    Content-Type: application/json

    {"name":"Alice"}

Deposit R$ 100.00 into the returned account:

    POST /accounts/{id}/deposits
    Content-Type: application/json

    {"amountMinor":10000,"idempotencyKey":"deposit-alice-001"}

Transfer R$ 25.00 to another account:

    POST /payments
    Content-Type: application/json

    {
      "sourceAccountId": "SOURCE_ACCOUNT_ID",
      "destinationAccountId": "DESTINATION_ACCOUNT_ID",
      "amountMinor": 2500,
      "idempotencyKey": "transfer-001"
    }

Repeating a request with the same idempotency key and payload returns the original payment. Reusing that key with a different payload returns HTTP 409. Every successful payment writes two ledger entries whose amounts sum to zero. The demo treasury can have a negative balance; regular accounts cannot transfer more than their current balance.

Account names are trimmed and must contain 1 to 100 characters. Amounts must be positive whole minor units. Idempotency keys must contain 1 to 100 characters and are case sensitive. Names and keys cannot contain control characters. Account and payment IDs must be non-empty GUIDs. Source and destination accounts must differ.

Errors use `application/problem+json` and include a stable `code` property alongside `type`, `title`, `status`, and `detail`. Invalid requests return `400` with `invalid_request`. Missing accounts and payments return `404` with `resource_not_found`. Reusing an idempotency key for another payment returns `409` with `idempotency_key_conflict`; insufficient balance returns `409` with `insufficient_funds`. Malformed JSON also returns a `400` problem response.

Example error:

    {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Invalid request","status":400,"detail":"amountMinor must be greater than zero.","code":"invalid_request"}

## Next backend milestones

1. Model payment states and simulated provider callbacks.
2. Add reconciliation against a provider settlement CSV.
3. Add audit queries and operational observability.
4. Add an Angular interface after the backend flow is complete.
