---
id: T3
parent: S01
milestone: M001
provides:
  - EF Core mappings for principal auth-state fields plus new auth persistence tables/indexes with snake_case naming and normalized-email uniqueness constraints
key_files:
  - backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/AdminUserConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/AuthSessionConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/PasswordResetTokenConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/RentACarDbContext.cs
  - backend/src/RentACar.Core/Interfaces/IApplicationDbContext.cs
  - backend/tests/RentACar.Tests/Integration/Data/DbContextTests.cs
  - .gsd/milestones/M001/slices/S01/S01-PLAN.md
key_decisions:
  - D006: EF auth persistence maps principal discriminator as string and enforces unique normalized-email indexes for both principal tables
patterns_established:
  - Keep auth schema mapping explicit via per-entity EF configurations (snake_case columns + named indexes) and validate with model-metadata integration tests
observability_surfaces:
  - `DbContextTests` now inspects EF model metadata for auth columns/tables/indexes (`normalized_email`, lockout counters/timestamps, token version, auth session/reset-token indexes)
  - Slice verification commands remain the primary diagnostics surface for auth failure-shape and schema mapping regressions
duration: 47m
verification_result: passed
completed_at: 2026-03-15T12:17:00+03:00
blocker_discovered: false
---

# T3: Map EF Core configuration and indexes

**Mapped auth-state fields and new auth persistence entities into EF Core with snake_case schema + index constraints, backed by model-level integration tests.**

## What Happened

Implemented EF persistence foundation for auth domain changes introduced in T1/T2:

- Extended principal mappings:
  - `CustomerConfiguration` now maps `normalized_email`, `failed_login_count`, `lockout_end_utc`, `last_login_at_utc`, `token_version`
  - `AdminUserConfiguration` now maps the same auth-state fields
  - Added unique normalized-email indexes:
    - `ux_customers_normalized_email`
    - `ux_admin_users_normalized_email`
- Added new EF configurations:
  - `AuthSessionConfiguration` (`auth_sessions`) with principal discriminator mapping, hash-at-rest token column mapping, and lifecycle indexes
  - `PasswordResetTokenConfiguration` (`password_reset_tokens`) with principal discriminator mapping, hash-at-rest token mapping, and lifecycle indexes
- Registered new sets in context contract:
  - `RentACarDbContext`: `AuthSessions`, `PasswordResetTokens`
  - `IApplicationDbContext`: `AuthSessions`, `PasswordResetTokens`
- Added integration assertions in `DbContextTests` to verify model metadata for:
  - principal auth-state column names
  - normalized-email unique indexes
  - auth session/reset token table names, column names, and key indexes

TDD flow used for T3:
1. Added new `DbContextTests` assertions first.
2. Ran `DbContextTests` and observed expected failures (missing entity registration/mapping).
3. Implemented EF mappings/configuration changes.
4. Re-ran tests to green.

## Verification

Red phase evidence:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~DbContextTests"`
  - **Failed (expected)** before implementation:
    - `AuthSessionAndPasswordResetTokenEntities_ShouldBeRegisteredWithSnakeCaseAndIndexes` (entity not registered)
    - `CustomerAndAdminEntities_ShouldMapNormalizedAuthStateColumns` (column naming mismatch for unmapped auth fields)

Green phase + required slice checks:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~DbContextTests"` → **Passed** (4/4)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests"` → **Passed** (5/5)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithMissingCredentials_ReturnsBadRequest"` → **Passed** (1/1)

## Diagnostics

- Schema/mapping diagnostics are now directly inspectable via:
  - EF model metadata checks in `backend/tests/RentACar.Tests/Integration/Data/DbContextTests.cs`
  - Index/column naming in configuration files under `backend/src/RentACar.Infrastructure/Data/Configurations/`
- Failure-path diagnostic surface remains intact via:
  - `AdminAuthControllerTests.Login_WithMissingCredentials_ReturnsBadRequest`
- Redaction constraints preserved:
  - auth persistence maps only hash fields (`refresh_token_hash`, `token_hash`) and no plaintext token fields were introduced.

## Deviations

- The dispatch-referenced task plan file (`.gsd/milestones/M001/slices/S01/tasks/T3-PLAN.md`) was not present in workspace, so execution followed the authoritative slice contract in `.gsd/milestones/M001/slices/S01/S01-PLAN.md` plus task objective.

## Known Issues

- None in T3 scope.

## Files Created/Modified

- `backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs` — mapped customer auth-state columns and added unique normalized-email index.
- `backend/src/RentACar.Infrastructure/Data/Configurations/AdminUserConfiguration.cs` — mapped admin auth-state columns and added unique normalized-email index.
- `backend/src/RentACar.Infrastructure/Data/Configurations/AuthSessionConfiguration.cs` — added snake_case mapping and indexes for auth session persistence.
- `backend/src/RentACar.Infrastructure/Data/Configurations/PasswordResetTokenConfiguration.cs` — added snake_case mapping and indexes for password reset token persistence.
- `backend/src/RentACar.Infrastructure/Data/RentACarDbContext.cs` — registered `AuthSessions` and `PasswordResetTokens` DbSets.
- `backend/src/RentACar.Core/Interfaces/IApplicationDbContext.cs` — exposed `AuthSessions` and `PasswordResetTokens` sets on app DB contract.
- `backend/tests/RentACar.Tests/Integration/Data/DbContextTests.cs` — added EF model-metadata assertions for new auth mappings/indexes.
- `.gsd/milestones/M001/slices/S01/S01-PLAN.md` — marked T3 complete.
- `.gsd/DECISIONS.md` — appended D006.
- `.gsd/STATE.md` — advanced next action to T4.
