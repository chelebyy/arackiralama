# S04: Admin Auth, RBAC & Password Reset Backend

**Goal:** close backend auth scope for admin/superadmin and password reset with stable contracts for S05
**Demo:** admin/superadmin auth lifecycle, superadmin admin-user management, and password-reset request/confirm flows all pass with non-enumerating failures, DB-backed session/reset state transitions, and explicit RBAC policy coverage

## Decomposition Rationale

I am grouping S04 into three tasks because the highest risk is stateful auth ordering and contract ambiguity:

1. **Admin lifecycle parity first (T01)** — AUTH-07 is a hard dependency for S05 admin login; we should align admin login/refresh/logout semantics with S03 before adding more surfaces.
2. **Password reset contract second (T02)** — AUTH-06 has sensitive security constraints (non-enumeration, single-use tokens, global invalidation). It should land after admin lifecycle primitives are consistent.
3. **SuperAdmin management and RBAC closure last (T03)** — AUTH-08/09 depends on stable admin identity/session behavior and final policy wiring checks across all role surfaces.

Verification is test-first at controller/service-contract level (existing project pattern), with explicit failure-path checks (lockout, replay/revocation, expired/consumed reset token, forbidden policy surfaces) so regressions are diagnosable from persisted auth state.

## Must-Haves

- Admin login resolves identity via normalized email, enforces 5-fail/15-minute lockout, and issues session-backed JWT+refresh cookie.
- Admin refresh rotates sessions with lineage (`revoked_at_utc`, `replaced_by_session_id`) and rejects replay/revoked/expired branches.
- Admin logout revokes server-side session by authenticated `sid` context and clears refresh cookie.
- Password reset request/confirm endpoints support principal-aware flows (`Customer`/`Admin`) with non-enumerating responses.
- Password reset tokens are hash-at-rest, single-use, expiry-validated, and consumed atomically on successful confirm.
- Successful password reset bumps principal `token_version` and revokes active sessions for that principal.
- Password-reset email dispatch has a callable backend contract path with testable invocation.
- SuperAdmin-only admin-user management endpoints exist (list/create/update role/activate-deactivate/reset trigger scope).
- RBAC policy matrix is explicitly verified for Guest/Customer/Admin/SuperAdmin protected endpoints.

## Requirement Coverage

- **Owned by S04 (from roadmap active scope):** AUTH-06, AUTH-07, AUTH-08, AUTH-09

Task mapping:

- **T01** → AUTH-07, AUTH-09 (admin auth lifecycle + admin-protected session behavior)
- **T02** → AUTH-06, AUTH-09 (password reset lifecycle + non-enumerating contract + token/session invalidation)
- **T03** → AUTH-08, AUTH-09 (superadmin management endpoints + policy matrix closure)

## Proof Level

- This slice proves: integration
- Real runtime required: yes
- Human/UAT required: no

## Verification

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests|FullyQualifiedName~AccessTokenSessionValidatorTests"`
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PasswordResetControllerTests"`
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminUsersControllerTests|FullyQualifiedName~RbacPolicyMatrixTests|FullyQualifiedName~AuthConventionsTests"`
- Verify observability/failure-path proof by ensuring tests assert persisted transitions on `admin_users.failed_login_count/lockout_end_utc/token_version`, `auth_sessions.revoked_at_utc/replaced_by_session_id`, and `password_reset_tokens.expires_at_utc/consumed_at_utc`.

## Observability / Diagnostics

- Runtime signals: admin lockout counters, token-version changes, session rotation/revocation lineage, password reset token lifecycle (`created` → `consumed`/`expired`), and dispatch invocation events.
- Inspection surfaces: controller/unit tests, `admin_users`, `auth_sessions`, `password_reset_tokens` tables, and mocked email-dispatch verification.
- Failure visibility: explicit branches for invalid credentials, lockout, replay/revoked/expired refresh, invalid/expired/consumed reset tokens, and forbidden superadmin endpoints.
- Redaction constraints: never expose plaintext password, refresh token, reset token, token hashes, or JWT secret in logs/responses/tests.

## Integration Closure

- Upstream surfaces consumed: `IJwtTokenService`, `IRefreshTokenCookieService`, `IAccessTokenSessionValidator`, auth claim/role/policy constants, `PasswordResetToken` entity/configuration, generic unauthorized contract.
- New wiring introduced in this slice: admin refresh/session-revoking logout paths, principal-aware password-reset controller + email-dispatch abstraction registration, superadmin admin-user management controller contracts.
- What remains before the milestone is truly usable end-to-end: S05 frontend integration for admin/customer auth UI, forgot/reset UX, and route guards.

## Tasks

- [x] **T01: Close admin auth lifecycle parity with lockout + refresh rotation** `est:2h`
  - Why: AUTH-07 fails without full admin lifecycle parity; S05 also depends on deterministic refresh/logout semantics identical to customer behavior.
  - Files: `backend/src/RentACar.API/Controllers/AdminAuthController.cs`, `backend/src/RentACar.API/Contracts/Auth/AdminRefreshResponse.cs`, `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs`, `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs`
  - Do: switch admin login lookup to `NormalizedEmail`, add lockout mutations (5 attempts/15 min) on admin principal state, implement admin refresh endpoint with hashed-cookie lookup + session replacement lineage, and update logout to revoke DB session by authenticated `sid`/principal before cookie clear while keeping unauthorized responses generic.
  - Verify: `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests|FullyQualifiedName~AccessTokenSessionValidatorTests"`
  - Done when: admin login/refresh/logout tests prove normalized-email login success, lockout transitions, replay-safe refresh rejection, and server-side logout revocation.

- [x] **T02: Implement principal-aware password reset request/confirm with email dispatch contract** `est:2h30m`
  - Why: AUTH-06 requires secure reset lifecycle closure and non-enumerating behavior; this is the highest security-risk branch after session lifecycle.
  - Files: `backend/src/RentACar.API/Controllers/PasswordResetController.cs`, `backend/src/RentACar.API/Contracts/Auth/PasswordResetRequest.cs`, `backend/src/RentACar.API/Contracts/Auth/PasswordResetConfirmRequest.cs`, `backend/src/RentACar.API/Services/IPasswordResetEmailDispatcher.cs`, `backend/src/RentACar.API/Services/PasswordResetEmailDispatcher.cs`, `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs`, `backend/tests/RentACar.Tests/Unit/Controllers/PasswordResetControllerTests.cs`
  - Do: add reset request/confirm endpoints that accept principal scope (`Customer`/`Admin`), persist only hashed reset tokens with expiry, always return non-enumerating request response, consume token once on confirm, reset password via `IPasswordHasher`, bump principal `TokenVersion`, revoke active sessions for that principal, and invoke email-dispatch abstraction on request path.
  - Verify: `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PasswordResetControllerTests"`
  - Done when: tests pass for request non-enumeration, dispatch invocation path, valid confirm success, expired/consumed/invalid token rejection, and post-reset token-version/session invalidation.

- [x] **T03: Add SuperAdmin admin-user management endpoints and RBAC matrix proofs** `est:2h`
  - Why: AUTH-08/AUTH-09 remain open until `SuperAdminOnly` is applied to real management surfaces and policy coverage is explicitly test-locked.
  - Files: `backend/src/RentACar.API/Controllers/AdminUsersController.cs`, `backend/src/RentACar.API/Contracts/Auth/AdminUserCreateRequest.cs`, `backend/src/RentACar.API/Contracts/Auth/AdminUserUpdateRoleRequest.cs`, `backend/src/RentACar.API/Contracts/Auth/AdminUserDto.cs`, `backend/tests/RentACar.Tests/Unit/Controllers/AdminUsersControllerTests.cs`, `backend/tests/RentACar.Tests/Unit/Controllers/RbacPolicyMatrixTests.cs`, `backend/tests/RentACar.Tests/Unit/Services/AuthConventionsTests.cs`
  - Do: implement superadmin-only admin-user management APIs (list/create/update role/activate-deactivate and reset-initiation hook as scoped), enforce role constants (`Admin`/`SuperAdmin`) without string drift, and add policy-matrix tests that assert endpoint-policy assignment across guest/customer/admin/superadmin boundaries.
  - Verify: `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminUsersControllerTests|FullyQualifiedName~RbacPolicyMatrixTests|FullyQualifiedName~AuthConventionsTests"`
  - Done when: superadmin management contracts are test-covered, non-superadmin access is denied by policy metadata/tests, and AUTH-08/09 backend policy closure is demonstrable.

## Files Likely Touched

- `backend/src/RentACar.API/Controllers/AdminAuthController.cs`
- `backend/src/RentACar.API/Contracts/Auth/AdminRefreshResponse.cs`
- `backend/src/RentACar.API/Controllers/PasswordResetController.cs`
- `backend/src/RentACar.API/Contracts/Auth/PasswordResetRequest.cs`
- `backend/src/RentACar.API/Contracts/Auth/PasswordResetConfirmRequest.cs`
- `backend/src/RentACar.API/Services/IPasswordResetEmailDispatcher.cs`
- `backend/src/RentACar.API/Services/PasswordResetEmailDispatcher.cs`
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs`
- `backend/src/RentACar.API/Controllers/AdminUsersController.cs`
- `backend/src/RentACar.API/Contracts/Auth/AdminUserCreateRequest.cs`
- `backend/src/RentACar.API/Contracts/Auth/AdminUserUpdateRoleRequest.cs`
- `backend/src/RentACar.API/Contracts/Auth/AdminUserDto.cs`
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs`
- `backend/tests/RentACar.Tests/Unit/Controllers/PasswordResetControllerTests.cs`
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminUsersControllerTests.cs`
- `backend/tests/RentACar.Tests/Unit/Controllers/RbacPolicyMatrixTests.cs`
- `backend/tests/RentACar.Tests/Unit/Services/AuthConventionsTests.cs`
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs`
