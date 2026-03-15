---
estimated_steps: 4
estimated_files: 7
---

# T01: Make Customer credential-ready and harden normalized identity lookups

**Slice:** S03 — Customer Auth API
**Milestone:** M001

## Description

Prepare persistence and domain invariants so customer auth can be implemented safely: add credential storage to customer records, preserve reservation-created customer compatibility, and remove case-sensitive customer lookup drift that can cause duplicate identities.

## Steps

1. Update `Customer` domain model to include credential storage semantics suitable for auth (without breaking existing reservation-created customers).
2. Update EF configuration and generate migration artifacts to add customer credential persistence in PostgreSQL while preserving existing rows.
3. Update reservation customer lookup/create path to query by normalized email rather than raw email.
4. Extend/adjust unit tests to lock email-normalization and customer-lookup invariants.

## Must-Haves

- [ ] `customers` schema persists customer credential material (`password_hash`) with compatibility for pre-auth historical rows.
- [ ] Reservation customer matching is case-insensitive via `NormalizedEmail` and no longer uses raw `Email` equality.
- [ ] Tests cover normalization + customer lookup behavior and pass.

## Verification

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests|FullyQualifiedName~ReservationServiceTests"`
- Migration compiles as part of test build and snapshot is updated coherently.

## Inputs

- `backend/src/RentACar.Core/Entities/Customer.cs` — current customer auth-state fields and missing credential field.
- `backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs` — current customer persistence mapping/indexes.
- `backend/src/RentACar.API/Services/ReservationService.cs` — current case-sensitive customer lookup path.
- `.gsd/milestones/M001/slices/S03/S03-PLAN.md` — task acceptance and requirement mapping.

## Expected Output

- `backend/src/RentACar.Core/Entities/Customer.cs` — credential-ready customer entity (auth-compatible).
- `backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs` — updated mapping for credential persistence.
- `backend/src/RentACar.Infrastructure/Data/Migrations/*_AddCustomerAuthCredentials.cs` — migration implementing customer credential column change.
- `backend/src/RentACar.Infrastructure/Data/Migrations/RentACarDbContextModelSnapshot.cs` — snapshot aligned with migration.
- `backend/src/RentACar.API/Services/ReservationService.cs` — normalized-email customer lookup.
- `backend/tests/RentACar.Tests/Unit/Entities/PrincipalAuthStateTests.cs` — updated invariants for customer auth fields.
- `backend/tests/RentACar.Tests/Unit/Services/ReservationServiceTests.cs` — assertions for normalized lookup behavior.

## Observability Impact

- **Signals changed:** `customers.password_hash` persistence becomes explicit (nullable for legacy reservation-created rows), and reservation customer resolution now keys on `NormalizedEmail` instead of raw `Email` casing.
- **How to inspect:** run `PrincipalAuthStateTests` and `ReservationServiceTests`; inspect generated migration + model snapshot for nullable `password_hash`; and review reservation flow behavior for same-email-different-case requests mapping to one customer.
- **Failure visibility:** case-drift duplicate-customer creation becomes detectable via failing reservation service tests and by observing multiple `customers` rows sharing the same normalized email when lookup logic regresses.
