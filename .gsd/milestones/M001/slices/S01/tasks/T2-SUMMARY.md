---
id: T2
parent: S01
milestone: M001
provides:
  - Shared auth persistence entities for session revocation and password-reset token lifecycle with hash-at-rest storage fields
key_files:
  - backend/src/RentACar.Core/Enums/AuthPrincipalType.cs
  - backend/src/RentACar.Core/Entities/AuthSession.cs
  - backend/src/RentACar.Core/Entities/PasswordResetToken.cs
  - backend/tests/RentACar.Tests/Unit/Entities/AuthPersistenceModelsTests.cs
  - .gsd/milestones/M001/slices/S01/S01-PLAN.md
key_decisions:
  - D005: shared principal-discriminated auth persistence models with hash-at-rest token fields
patterns_established:
  - Model auth session/reset state with `PrincipalType + PrincipalId` so customer/admin remain separate principals while sharing persistence workflows
  - Represent security-sensitive token material only as hash fields (`RefreshTokenHash`, `TokenHash`) and validate lifecycle via explicit entity methods/tests
observability_surfaces:
  - `AuthSession.IsActive/IsRevoked` and `PasswordResetToken.IsActive/TryConsume` expose inspectable lifecycle state for tests and diagnostics
  - `AuthPersistenceModelsTests` verifies active/expired/revoked/consumed transitions and plaintext-token property absence
duration: 34m
verification_result: passed
completed_at: 2026-03-15T11:53:44+03:00
blocker_discovered: false
---

# T2: Add persistence models for sessions and password reset

**Added `AuthSession` and `PasswordResetToken` domain persistence models (with shared principal typing and hash-at-rest token fields) plus unit coverage for revocation/expiry/single-use behavior.**

## What Happened

Implemented new auth persistence foundation entities in `RentACar.Core`:

- Added `AuthPrincipalType` enum (`Customer`, `Admin`) for explicit principal discrimination without merging principal tables.
- Added `AuthSession` entity with:
  - `PrincipalType`, `PrincipalId`
  - `RefreshTokenHash` (hash-at-rest token field)
  - `RefreshTokenExpiresAtUtc`, `LastSeenAtUtc`, `RevokedAtUtc`, `ReplacedBySessionId`, `CreatedByIp`, `UserAgent`
  - helper signals: `IsRevoked`, `IsActive(DateTime utcNow)`
- Added `PasswordResetToken` entity with:
  - `PrincipalType`, `PrincipalId`
  - `TokenHash` (hash-at-rest token field)
  - `ExpiresAtUtc`, `ConsumedAtUtc`
  - helper signals: `IsConsumed`, `IsActive(DateTime utcNow)`, `TryConsume(DateTime consumedAtUtc)` for single-use behavior.

Added `AuthPersistenceModelsTests` to verify:
- hashed token field usage and active-state semantics
- revoked/expired session behavior
- single-use reset-token consumption
- expired reset-token rejection
- absence of plaintext token properties (`RefreshToken`, `Token`) on entities.

Tracking artifacts updated:
- marked T2 complete in `.gsd/milestones/M001/slices/S01/S01-PLAN.md`
- appended D005 to `.gsd/DECISIONS.md`
- updated `.gsd/STATE.md` next action to T3.

## Verification

Executed with fresh runs:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AuthPersistenceModelsTests"`  
  Result: **Passed** (5/5)

Slice verification commands (required by S01 plan):

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests"`  
  Result: **Passed** (5/5)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithMissingCredentials_ReturnsBadRequest"`  
  Result: **Passed** (1/1)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~DbContextTests"`  
  Result: **Passed** (2/2)

## Diagnostics

- Entity lifecycle signals are directly inspectable in code/tests:
  - `AuthSession.IsActive(...)` and `AuthSession.IsRevoked`
  - `PasswordResetToken.IsActive(...)`, `PasswordResetToken.IsConsumed`, `PasswordResetToken.TryConsume(...)`
- Redaction surface check is test-backed (`AuthPersistenceModelsTests.AuthEntities_ShouldNotExposePlaintextTokenProperties`) to keep persistence contract hash-only.

## Deviations

- The authoritative per-task file referenced by dispatch (`.gsd/milestones/M001/slices/S01/tasks/T2-PLAN.md`) was not present in workspace; execution followed the slice contract in `S01-PLAN.md` and recorded outputs accordingly.

## Known Issues

- None for T2 scope.

## Files Created/Modified

- `backend/src/RentACar.Core/Enums/AuthPrincipalType.cs` — added shared principal discriminator enum for auth persistence records.
- `backend/src/RentACar.Core/Entities/AuthSession.cs` — added session persistence model with hash-at-rest refresh token and revocation/expiry signals.
- `backend/src/RentACar.Core/Entities/PasswordResetToken.cs` — added password-reset persistence model with hash-at-rest token and single-use consumption behavior.
- `backend/tests/RentACar.Tests/Unit/Entities/AuthPersistenceModelsTests.cs` — added unit coverage for session/reset token lifecycle and plaintext-token surface guards.
- `.gsd/milestones/M001/slices/S01/S01-PLAN.md` — marked T2 as complete.
- `.gsd/DECISIONS.md` — appended D005 architectural decision.
- `.gsd/STATE.md` — updated recent decisions and next action for slice progression.
