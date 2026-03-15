---
id: T01
parent: S04
milestone: M001
provides:
  - Admin auth lifecycle parity with customer semantics: normalized-email login, deterministic lockout mutation, refresh rotation lineage, and server-side logout revocation
key_files:
  - backend/src/RentACar.API/Controllers/AdminAuthController.cs
  - backend/src/RentACar.API/Contracts/Auth/AdminRefreshResponse.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs
  - backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs
key_decisions:
  - D018: Admin auth lifecycle mirrors customer session/lockout semantics with claim-bound logout revocation
patterns_established:
  - Admin login identity resolution uses `AdminUser.NormalizeEmail` / `NormalizedEmail` lookups only
  - Admin refresh accepts only active non-revoked/non-replaced/non-expired sessions and rotates lineage via `revoked_at_utc` + `replaced_by_session_id`
  - Admin logout rejects missing-claim/session-mismatch contexts with generic unauthorized while still clearing refresh cookie
observability_surfaces:
  - `admin_users.failed_login_count`, `admin_users.lockout_end_utc`, `admin_users.last_login_at_utc`
  - `auth_sessions.revoked_at_utc`, `auth_sessions.replaced_by_session_id` for admin principal
  - `AdminAuthControllerTests` branches asserting lockout/replay/revocation/mismatch behavior
  - `AccessTokenSessionValidatorTests.ValidateAsync_WhenAdminSessionIsReplaced_ReturnsSessionRevoked`
duration: 52m
verification_result: passed
completed_at: 2026-03-15
blocker_discovered: false
---

# T01: Close admin auth lifecycle parity with lockout + refresh rotation

**Admin auth now ships full DB-backed lifecycle parity with customer auth: normalized email login + deterministic lockout mutation, refresh rotation lineage, and claim-bound logout revocation.**

## What Happened

Implemented `AdminAuthController` parity with S03 customer lifecycle behavior:
- Login now resolves by `NormalizedEmail`, enforces 5-fail / 15-minute lockout mutation, clears stale lockouts, and resets counters on successful auth.
- Added `POST /api/admin/v1/auth/refresh` with cookie-token lookup, hash-at-rest session validation, replay/revoked/expired rejection, and rotation to a new `AuthSession` row while revoking/linking the old row.
- Upgraded logout to async DB-backed revocation using authenticated principal/session claims (`sub`/`sid` fallback to `nameidentifier`/`ClaimTypes.Sid`), returning generic unauthorized on missing claims or session mismatch before cookie clear.
- Added `AdminRefreshResponse` contract and expanded `AdminAuthControllerTests` for normalized-email success, lockout progression, active-lockout denial, refresh success/replay/expiry failures, and logout revocation/mismatch branches.
- Extended session-validator coverage with an admin-specific replaced-session guard test.

## Verification

Executed slice and task verification commands:
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests|FullyQualifiedName~AccessTokenSessionValidatorTests"` ✅ (19 passed, 0 failed)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PasswordResetControllerTests"` ⚠️ no matching tests yet (expected for pre-T02 state)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminUsersControllerTests|FullyQualifiedName~RbacPolicyMatrixTests|FullyQualifiedName~AuthConventionsTests"` ✅ (6 passed, 0 failed)

Observability-impact proof (from assertions):
- `admin_users.failed_login_count` increments on failed login and resets on successful login.
- `admin_users.lockout_end_utc` is set on threshold and cleared on success/expired-lockout recovery.
- `admin_users.last_login_at_utc` is persisted on successful login.
- `auth_sessions.revoked_at_utc` + `replaced_by_session_id` transitions are asserted on admin refresh rotation and logout revocation.

## Diagnostics

Fastest inspection surfaces:
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs`
  - `Login_WithInvalidPassword_TracksFailuresAndLocksOnFifthAttempt`
  - `Refresh_WithActiveSession_RotatesSessionAndIssuesNewTokens`
  - `Refresh_WithReplayedRefreshToken_ReturnsUnauthorizedGenericPayload`
  - `Logout_WithSessionMismatch_ReturnsUnauthorizedAndClearsCookie`
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs`
  - `ValidateAsync_WhenAdminSessionIsReplaced_ReturnsSessionRevoked`

## Deviations

None.

## Known Issues

- `PasswordResetControllerTests` suite is not present yet (planned for T02), so the slice-level password-reset verification filter currently returns “no matching tests”.

## Files Created/Modified

- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — added normalized login, lockout mutations, refresh rotation endpoint, and claim/session-bound logout revocation.
- `backend/src/RentACar.API/Contracts/Auth/AdminRefreshResponse.cs` — added admin refresh success contract.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs` — replaced with full admin lifecycle parity coverage (success + failure branches).
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs` — added admin-specific replaced-session revocation guard coverage.
- `.gsd/DECISIONS.md` — appended D018 for admin lifecycle parity decision.
- `.gsd/milestones/M001/slices/S04/S04-PLAN.md` — marked T01 complete.
