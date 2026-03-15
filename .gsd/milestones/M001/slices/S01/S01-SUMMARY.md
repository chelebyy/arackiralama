---
id: S01
parent: M001
milestone: M001
provides:
  - Auth domain and persistence foundation with principal auth-state fields, hash-at-rest token records, EF mappings/indexes, and a safe rollout migration
requires: []
affects:
  - S02
  - S03
  - S04
key_files:
  - backend/src/RentACar.Core/Entities/Customer.cs
  - backend/src/RentACar.Core/Entities/AdminUser.cs
  - backend/src/RentACar.Core/Entities/AuthSession.cs
  - backend/src/RentACar.Core/Entities/PasswordResetToken.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/AdminUserConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/AuthSessionConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/PasswordResetTokenConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/Migrations/20260315090214_Phase6AuthDomainPersistenceFoundation.cs
  - backend/src/RentACar.Infrastructure/Data/Migrations/RentACarDbContextModelSnapshot.cs
  - backend/tests/RentACar.Tests/Unit/Entities/PrincipalAuthStateTests.cs
  - backend/tests/RentACar.Tests/Unit/Entities/AuthPersistenceModelsTests.cs
  - backend/tests/RentACar.Tests/Integration/Data/DbContextTests.cs
key_decisions:
  - D004: Principal entities auto-normalize email on assignment
  - D005: Auth persistence uses shared principal-discriminated token records with hash-at-rest fields only
  - D006: EF maps principal discriminator as string and enforces unique normalized-email indexes for both principal tables
  - D007: Migration backfills normalized emails and fails fast on duplicate normalized principals before unique-index enforcement
patterns_established:
  - Keep `Email` and `NormalizedEmail` synchronized in entity setters to enforce normalization invariants at the domain boundary
  - Use `PrincipalType + PrincipalId` for shared auth persistence workflows while keeping customer/admin roots separate
  - Map auth schema explicitly with snake_case columns and named indexes; verify via EF metadata tests
  - Roll out identity-hardening changes via nullable add -> deterministic backfill -> duplicate guard -> NOT NULL + unique index
observability_surfaces:
  - Unit tests (`PrincipalAuthStateTests`, `AuthPersistenceModelsTests`) for normalization/defaults and lifecycle transitions
  - Integration tests (`DbContextTests`) for auth columns/tables/indexes and snake_case mapping
  - Failure-path API test (`AdminAuthControllerTests.Login_WithMissingCredentials_ReturnsBadRequest`) for structured auth diagnostic surface
  - Migration SQL duplicate guards and hints in `Phase6AuthDomainPersistenceFoundation`
drill_down_paths:
  - .gsd/milestones/M001/slices/S01/tasks/T1-SUMMARY.md
  - .gsd/milestones/M001/slices/S01/tasks/T2-SUMMARY.md
  - .gsd/milestones/M001/slices/S01/tasks/T3-SUMMARY.md
  - .gsd/milestones/M001/slices/S01/tasks/T4-SUMMARY.md
duration: 2h49m
verification_result: passed
completed_at: 2026-03-15T13:10:00+03:00
---

# S01: Auth Domain & Persistence Foundation

**Shipped the full auth persistence base: principal auth-state fields, hashed session/reset token entities, EF mappings/indexes, and a migration hardened for existing-email collision risks.**

## What Happened

S01 completed all four planned tasks end-to-end:

- **T1:** Extended `Customer` and `AdminUser` with auth-state fields (`normalized_email`, failed-login counter, lockout timestamp, last-login timestamp, token version) and enforced email normalization invariants in entity setters.
- **T2:** Added shared auth persistence entities (`AuthSession`, `PasswordResetToken`) using principal discrimination (`AuthPrincipalType`) and **hash-at-rest only** token storage semantics.
- **T3:** Mapped all new fields/entities into EF Core with snake_case columns, named indexes, and unique normalized-email constraints for both principal tables.
- **T4:** Generated and hardened migration rollout to safely handle existing records using deterministic backfill and duplicate-detection fail-fast checks before applying unique indexes.

This slice delivers the schema and model contract needed for upcoming JWT/session/refresh/reset logic in S02+ without introducing plaintext token persistence.

## Verification

Fresh slice-level verification executed and passed:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests"` → Passed (5/5)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithMissingCredentials_ReturnsBadRequest"` → Passed (1/1)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~DbContextTests"` → Passed (4/4)

Slice-final migration observability checks executed:

- `dotnet ef migrations script --project src/RentACar.Infrastructure --startup-project src/RentACar.API`
- Verified SQL contains:
  - normalized-email backfill (`UPPER(TRIM(email))`)
  - duplicate guard `RAISE EXCEPTION` blocks with actionable hints
  - `ALTER ... normalized_email SET NOT NULL`
  - creation of `auth_sessions` and `password_reset_tokens`
  - unique indexes `ux_customers_normalized_email` and `ux_admin_users_normalized_email`
- Verified model snapshot includes expected auth columns/tables/indexes in `RentACarDbContextModelSnapshot.cs`.

## Requirements Advanced

- **AUTH-04** — refresh-token persistence foundation delivered (`auth_sessions`, hashed refresh token storage, revocation lifecycle fields/indexes).
- **AUTH-05** — session revocation/logout-all persistence primitives delivered (`revoked_at_utc`, `replaced_by_session_id`, `token_version`).
- **AUTH-06** — password reset token lifecycle persistence delivered (`password_reset_tokens`, expiry + single-use consumption model).
- **AUTH-07** — admin principal auth-state persistence/normalization invariants delivered (`admin_users` auth columns + unique normalized email).
- **AUTH-10** — lockout state persistence delivered (`failed_login_count`, `lockout_end_utc`) for both principal types.

## Requirements Validated

- None — this slice establishes domain/persistence prerequisites but does not yet prove end-to-end auth API behavior.

## New Requirements Surfaced

- None.

## Requirements Invalidated or Re-scoped

- None.

## Deviations

- Per-task dispatch plan files (`T1-PLAN.md`...`T4-PLAN.md`) were absent; execution followed the authoritative slice contract in `S01-PLAN.md` and recorded outcomes in task summaries.

## Known Limitations

- JWT issuance/refresh service behavior is not implemented in this slice.
- Lockout policy enforcement logic is not yet wired into login handlers.
- Password-reset email delivery and user-facing reset endpoints are not yet implemented.

## Follow-ups

- S02: implement JWT/refresh token generation, rotation, revocation, and expiry policy over `auth_sessions`.
- S03/S04: wire customer/admin auth APIs to new persistence state and enforce lockout counters.
- S03+: implement password-reset issuance/consume flow and email delivery integration.

## Files Created/Modified

- `backend/src/RentACar.Core/Entities/Customer.cs` — auth-state fields + email/normalized-email invariant.
- `backend/src/RentACar.Core/Entities/AdminUser.cs` — auth-state fields + email/normalized-email invariant.
- `backend/src/RentACar.Core/Enums/AuthPrincipalType.cs` — shared principal discriminator.
- `backend/src/RentACar.Core/Entities/AuthSession.cs` — hashed refresh-token session persistence lifecycle.
- `backend/src/RentACar.Core/Entities/PasswordResetToken.cs` — hashed reset-token lifecycle + single-use consumption.
- `backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs` — auth columns + unique normalized-email index.
- `backend/src/RentACar.Infrastructure/Data/Configurations/AdminUserConfiguration.cs` — auth columns + unique normalized-email index.
- `backend/src/RentACar.Infrastructure/Data/Configurations/AuthSessionConfiguration.cs` — snake_case mapping + auth-session indexes.
- `backend/src/RentACar.Infrastructure/Data/Configurations/PasswordResetTokenConfiguration.cs` — snake_case mapping + reset-token indexes.
- `backend/src/RentACar.Infrastructure/Data/RentACarDbContext.cs` — `AuthSessions` + `PasswordResetTokens` DbSets.
- `backend/src/RentACar.Core/Interfaces/IApplicationDbContext.cs` — auth DbSet contracts.
- `backend/src/RentACar.Infrastructure/Data/Migrations/20260315090214_Phase6AuthDomainPersistenceFoundation.cs` — safe rollout migration.
- `backend/src/RentACar.Infrastructure/Data/Migrations/20260315090214_Phase6AuthDomainPersistenceFoundation.Designer.cs` — EF migration designer metadata.
- `backend/src/RentACar.Infrastructure/Data/Migrations/RentACarDbContextModelSnapshot.cs` — updated model snapshot.
- `backend/tests/RentACar.Tests/Unit/Entities/PrincipalAuthStateTests.cs` — principal auth-state/normalization tests.
- `backend/tests/RentACar.Tests/Unit/Entities/AuthPersistenceModelsTests.cs` — token/session lifecycle and plaintext-surface guard tests.
- `backend/tests/RentACar.Tests/Integration/Data/DbContextTests.cs` — EF mapping/index metadata assertions.

## Forward Intelligence

### What the next slice should know
- `token_version` exists on both principals and is ready to support global session invalidation/logout-all semantics.
- Migration is intentionally fail-fast for normalized-email duplicates; production rollout should run preflight duplicate queries first.
- `AuthPrincipalType` is persisted as string for readability; keep enum member names stable to avoid migration churn.

### What's fragile
- Email uniqueness now depends on normalized form; any future email mutation path must keep setter-based invariant and avoid bypass writes.
- Auth flows not yet wired: lockout counters and revocation fields exist but are not enforced by application services yet.

### Authoritative diagnostics
- `backend/tests/RentACar.Tests/Integration/Data/DbContextTests.cs` — fastest confidence check for mapping/index regressions.
- `backend/src/RentACar.Infrastructure/Data/Migrations/20260315090214_Phase6AuthDomainPersistenceFoundation.cs` — source of truth for rollout guards.
- `dotnet ef migrations script --project src/RentACar.Infrastructure --startup-project src/RentACar.API` — authoritative SQL for release verification.

### What assumptions changed
- “Migration can set normalized_email directly non-null with defaults” — replaced by explicit backfill + duplicate guard sequence to avoid unsafe rollout on historical data.
