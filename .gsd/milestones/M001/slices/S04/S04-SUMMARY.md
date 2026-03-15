---
id: S04
parent: M001
milestone: M001
provides:
  - Backend auth closure for admin/superadmin lifecycle, password-reset request/confirm, and RBAC policy-matrix verification with stable contracts for S05 frontend integration
requires:
  - slice: S03
    provides: customer auth session lineage conventions (`revoked_at_utc`, `replaced_by_session_id`), shared JWT claim constants, and generic unauthorized response contract
affects:
  - S05
key_files:
  - backend/src/RentACar.API/Controllers/AdminAuthController.cs
  - backend/src/RentACar.API/Contracts/Auth/AdminRefreshResponse.cs
  - backend/src/RentACar.API/Controllers/PasswordResetController.cs
  - backend/src/RentACar.API/Services/IPasswordResetEmailDispatcher.cs
  - backend/src/RentACar.API/Services/PasswordResetEmailDispatcher.cs
  - backend/src/RentACar.API/Controllers/AdminUsersController.cs
  - backend/src/RentACar.API/Authentication/AuthRoleNames.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/PasswordResetControllerTests.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/AdminUsersControllerTests.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/RbacPolicyMatrixTests.cs
  - backend/tests/RentACar.Tests/Unit/Services/AuthConventionsTests.cs
  - backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs
key_decisions:
  - D018: Admin auth lifecycle mirrors customer semantics with normalized-email lookup, lockout mutation, refresh rotation lineage, and claim-bound logout revocation
  - D019: Password-reset request persists hash-at-rest tokens and keeps non-enumerating success even when dispatch fails
  - D020: Canonical admin-role normalization flows through `AuthRoleNames.TryNormalizeAdminRole`, and SuperAdmin reset-initiation reuses secure reset-token/dispatch patterns
patterns_established:
  - Admin login/refresh/logout now follow the same stateful DB-backed lifecycle semantics as customer auth
  - Password reset is principal-aware (`Admin`/`Customer`), hash-at-rest, expiry-validated, single-use, and invalidates principal token/session state on confirm
  - SuperAdmin admin-user management accepts only canonical `Admin`/`SuperAdmin` role values and is protected by explicit RBAC policy assertions
observability_surfaces:
  - `admin_users.failed_login_count`, `admin_users.lockout_end_utc`, `admin_users.last_login_at_utc`, `admin_users.token_version`
  - `auth_sessions.revoked_at_utc`, `auth_sessions.replaced_by_session_id`
  - `password_reset_tokens.token_hash`, `password_reset_tokens.expires_at_utc`, `password_reset_tokens.consumed_at_utc`
  - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests|FullyQualifiedName~AccessTokenSessionValidatorTests"`
  - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PasswordResetControllerTests"`
  - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminUsersControllerTests|FullyQualifiedName~RbacPolicyMatrixTests|FullyQualifiedName~AuthConventionsTests"`
drill_down_paths:
  - .gsd/milestones/M001/slices/S04/tasks/T01-SUMMARY.md
  - .gsd/milestones/M001/slices/S04/tasks/T02-SUMMARY.md
  - .gsd/milestones/M001/slices/S04/tasks/T03-SUMMARY.md
duration: 3h10m
verification_result: passed
completed_at: 2026-03-15
---

# S04: Admin Auth, RBAC & Password Reset Backend

**Shipped full backend auth closure for admin/superadmin + password reset with persisted lifecycle diagnostics and RBAC policy-matrix proof for AUTH-06/07/08/09.**

## What Happened

S04 completed the remaining backend authentication scope in three integrated steps:

- **T01 (Admin lifecycle parity):** Admin login now resolves by normalized email and applies deterministic lockout mutations (5 failures / 15 minutes). Admin refresh now rotates DB sessions with explicit lineage (`revoked_at_utc`, `replaced_by_session_id`) and rejects replay/revoked/expired branches. Logout now revokes by authenticated `sid` + principal context before clearing the refresh cookie.
- **T02 (Password reset lifecycle):** Added principal-aware reset request/confirm endpoints for `Admin` and `Customer`. Request flow persists only hashed reset tokens and always returns a non-enumerating success payload. Confirm flow validates hash/expiry/consumed state, consumes token once, resets password hash, increments principal `token_version`, and revokes active sessions for that principal. Email dispatch is invoked through an injectable contract path.
- **T03 (SuperAdmin management + RBAC closure):** Added SuperAdmin-only admin-user management endpoints (list/create/update-role/activate/deactivate/reset-initiation). Canonical role parsing now rejects casing drift and allows only `Admin`/`SuperAdmin`. Added reflection-based RBAC matrix tests to lock endpoint policy assignments across Guest/Customer/Admin/SuperAdmin surfaces.

## Verification

Executed all slice-level verification commands from S04 plan with fresh output:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests|FullyQualifiedName~AccessTokenSessionValidatorTests"` ✅ **19 passed, 0 failed**
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PasswordResetControllerTests"` ✅ **7 passed, 0 failed**
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminUsersControllerTests|FullyQualifiedName~RbacPolicyMatrixTests|FullyQualifiedName~AuthConventionsTests"` ✅ **45 passed, 0 failed**

Observability/diagnostic surfaces were confirmed via these tests, including persisted transitions on:
- `admin_users.failed_login_count`, `admin_users.lockout_end_utc`, `admin_users.last_login_at_utc`, `admin_users.token_version`
- `auth_sessions.revoked_at_utc`, `auth_sessions.replaced_by_session_id`
- `password_reset_tokens.expires_at_utc`, `password_reset_tokens.consumed_at_utc`

## Requirements Advanced

- AUTH-06 — password reset request/confirm contract implemented with non-enumeration, hash-at-rest tokens, single-use consume, and session/token-version invalidation.
- AUTH-07 — admin login/refresh/logout lifecycle now production-closed with lockout, rotation lineage, and claim-bound revocation.
- AUTH-08 — SuperAdmin admin-user management APIs implemented and test-covered.
- AUTH-09 — RBAC policy closure established with endpoint policy-matrix assertions.

## Requirements Validated

- AUTH-06 — validated by `PasswordResetControllerTests` proving request non-enumeration, valid confirm success, and invalid/expired/consumed token rejection paths.
- AUTH-07 — validated by `AdminAuthControllerTests` + `AccessTokenSessionValidatorTests` proving normalized-email login, lockout transitions, refresh replay protection, and logout revocation/mismatch handling.
- AUTH-08 — validated by `AdminUsersControllerTests` proving create/list/update role/activate/deactivate/reset-initiation behavior under SuperAdmin-only controller policy.
- AUTH-09 — validated by `RbacPolicyMatrixTests` and `AuthConventionsTests` proving effective endpoint authorization boundaries and canonical role-policy conventions.

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none — auth requirement scope remained unchanged; S04 provided direct closure evidence.

## Deviations

none

## Known Limitations

- `PasswordResetEmailDispatcher` is currently a logging-backed executable stub. Real outbound provider/template integration is still required in a future notifications slice.

## Follow-ups

- S05 should consume the finalized backend contracts for admin login/refresh/logout and forgot/reset UX flows, including cookie/session expectations and RBAC guard behavior.

## Files Created/Modified

- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — completed admin lifecycle parity (normalized login, lockout, refresh rotation, claim-bound logout revocation).
- `backend/src/RentACar.API/Contracts/Auth/AdminRefreshResponse.cs` — added admin refresh response contract.
- `backend/src/RentACar.API/Controllers/PasswordResetController.cs` — added principal-aware reset request/confirm lifecycle.
- `backend/src/RentACar.API/Contracts/Auth/PasswordResetRequest.cs` — added reset request contract.
- `backend/src/RentACar.API/Contracts/Auth/PasswordResetConfirmRequest.cs` — added reset confirm contract.
- `backend/src/RentACar.API/Services/IPasswordResetEmailDispatcher.cs` — added dispatch abstraction.
- `backend/src/RentACar.API/Services/PasswordResetEmailDispatcher.cs` — added default dispatch implementation.
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — registered password reset dispatcher in DI.
- `backend/src/RentACar.API/Controllers/AdminUsersController.cs` — added SuperAdmin admin-user management endpoints.
- `backend/src/RentACar.API/Contracts/Auth/AdminUserCreateRequest.cs` — added admin create payload contract.
- `backend/src/RentACar.API/Contracts/Auth/AdminUserUpdateRoleRequest.cs` — added role update payload contract.
- `backend/src/RentACar.API/Contracts/Auth/AdminUserDto.cs` — added admin-user response DTO.
- `backend/src/RentACar.API/Authentication/AuthRoleNames.cs` — added canonical admin-role normalization helper.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs` — expanded admin lifecycle parity branch coverage.
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs` — added replaced-admin-session revocation check.
- `backend/tests/RentACar.Tests/Unit/Controllers/PasswordResetControllerTests.cs` — added full reset request/confirm path coverage.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminUsersControllerTests.cs` — added superadmin management endpoint tests.
- `backend/tests/RentACar.Tests/Unit/Controllers/RbacPolicyMatrixTests.cs` — added RBAC endpoint-policy matrix assertions.
- `backend/tests/RentACar.Tests/Unit/Services/AuthConventionsTests.cs` — added canonical role normalization/rejection coverage.
- `.gsd/REQUIREMENTS.md` — moved AUTH-06/07/08/09 from Active to Validated and checked requirement boxes.
- `.gsd/milestones/M001/M001-ROADMAP.md` — marked S04 complete.

## Forward Intelligence

### What the next slice should know
- S05 should treat reset responses as intentionally non-enumerating and handle generic success messages even when email is unknown/inactive.
- Admin refresh/logout semantics now depend on server session lineage and `sid` claim continuity, not client cookie state alone.

### What's fragile
- Password reset dispatch is stubbed (logging-only) — transport/provider wiring is not yet production-delivery capable.

### Authoritative diagnostics
- `backend/tests/RentACar.Tests/Unit/Controllers/PasswordResetControllerTests.cs` — fastest proof for reset token lifecycle and principal/session invalidation behavior.
- `backend/tests/RentACar.Tests/Unit/Controllers/RbacPolicyMatrixTests.cs` — fastest proof for policy drift across role boundaries.

### What assumptions changed
- Original assumption: role inputs could be case-insensitive aliases. — Actual behavior: only canonical `Admin`/`SuperAdmin` contract values are accepted to prevent role-string drift.
