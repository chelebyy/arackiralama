---
id: T01
parent: S03
milestone: M001
provides:
  - Customer credential-ready persistence and normalized reservation identity lookup invariants for auth follow-up tasks
key_files:
  - backend/src/RentACar.Core/Entities/Customer.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/Migrations/20260315100956_AddCustomerAuthCredentials.cs
  - backend/src/RentACar.API/Services/ReservationService.cs
  - backend/tests/RentACar.Tests/Unit/Entities/PrincipalAuthStateTests.cs
  - backend/tests/RentACar.Tests/Unit/Services/ReservationServiceTests.cs
key_decisions:
  - D014: Customer credential storage is nullable and reservation customer matching must use normalized email.
patterns_established:
  - Customer credentials are persisted with nullable `password_hash` to keep reservation-created legacy rows valid.
  - Reservation customer resolution normalizes incoming email and queries `NormalizedEmail` before creating a new customer row.
observability_surfaces:
  - `customers.password_hash` in migration + model snapshot
  - `PrincipalAuthStateTests` credential-state assertions (`PasswordHash`/`HasPassword`)
  - `ReservationServiceTests` normalized lookup regression test (`...EmailDiffersByCase...`)
duration: 1h
verification_result: passed
completed_at: 2026-03-15
blocker_discovered: false
---

# T01: Make Customer credential-ready and harden normalized identity lookups

**Added nullable customer credential persistence and switched reservation customer matching to normalized-email lookup, with tests locking both invariants.**

## What Happened

I first applied required pre-flight observability doc fixes in `S03-PLAN.md` and `T01-PLAN.md`.

Then I implemented T01 runtime changes:
- Extended `Customer` with nullable `PasswordHash` plus `HasPassword` semantics so pre-auth historical rows remain valid while auth readiness is explicit.
- Updated `CustomerConfiguration` to map `PasswordHash` to nullable `password_hash` (`varchar(256)`).
- Generated EF migration `20260315100956_AddCustomerAuthCredentials` and aligned snapshot/designer artifacts.
- Updated `ReservationService.GetOrCreateCustomerAsync` to normalize incoming email and query by `NormalizedEmail` instead of raw `Email` equality.
- Expanded tests:
  - `PrincipalAuthStateTests` now asserts secure credential defaults and `HasPassword` behavior.
  - `ReservationServiceTests` now asserts existing customers are reused when request email casing/spacing differs.

I also appended decision `D014` to `.gsd/DECISIONS.md`.

## Verification

Executed task-level verification:
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests|FullyQualifiedName~ReservationServiceTests"` ✅ (28 passed)

Executed slice-level verification commands (as required):
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests|FullyQualifiedName~ReservationServiceTests"` ✅
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~CustomerAuthControllerTests"` ⚠️ no tests matched filter yet
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AccessTokenSessionValidatorTests"` ✅ (6 passed)

Migration compiles as part of the above test build.

## Diagnostics

Future agents can inspect:
- Migration/schema surface: `backend/src/RentACar.Infrastructure/Data/Migrations/20260315100956_AddCustomerAuthCredentials.cs` and `RentACarDbContextModelSnapshot.cs` for nullable `customers.password_hash`.
- Domain behavior surface: `PrincipalAuthStateTests` for customer credential state defaults.
- Failure-path visibility for lookup drift: `ReservationServiceTests.CreateDraftReservationAsync_WhenExistingCustomerEmailDiffersByCase_ReusesExistingCustomerByNormalizedEmail` (fails if raw email equality is reintroduced).

## Deviations

- None.

## Known Issues

- `CustomerAuthControllerTests` filter currently matches no tests in this repo state; expected to be addressed by subsequent slice tasks (T02/T03).

## Files Created/Modified

- `.gsd/milestones/M001/slices/S03/S03-PLAN.md` — added failure-path verification guidance and marked T01 as done.
- `.gsd/milestones/M001/slices/S03/tasks/T01-PLAN.md` — added `## Observability Impact` section (pre-flight requirement).
- `backend/src/RentACar.Core/Entities/Customer.cs` — added nullable `PasswordHash` and `HasPassword` semantics.
- `backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs` — mapped nullable `password_hash` column.
- `backend/src/RentACar.Infrastructure/Data/Migrations/20260315100956_AddCustomerAuthCredentials.cs` — added migration for `customers.password_hash`.
- `backend/src/RentACar.Infrastructure/Data/Migrations/20260315100956_AddCustomerAuthCredentials.Designer.cs` — generated migration model.
- `backend/src/RentACar.Infrastructure/Data/Migrations/RentACarDbContextModelSnapshot.cs` — snapshot aligned with new customer credential column.
- `backend/src/RentACar.API/Services/ReservationService.cs` — switched customer lookup to `NormalizedEmail`.
- `backend/tests/RentACar.Tests/Unit/Entities/PrincipalAuthStateTests.cs` — added customer credential-state assertions.
- `backend/tests/RentACar.Tests/Unit/Services/ReservationServiceTests.cs` — added normalized-email reuse regression test.
- `.gsd/DECISIONS.md` — appended D014.
- `.gsd/STATE.md` — advanced next action to T02.
