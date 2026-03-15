---
id: S02
parent: M001
milestone: M001
provides:
  - Shared JWT/session infrastructure for admin and customer principals with 15-minute access tokens (`sid`/`ver` claims), 7-day refresh-token primitives, DB-backed session validation, and secure refresh-cookie conventions.
requires:
  - slice: S01
    provides: Auth domain entities, persistence mappings/migration, and principal token-version/session data model foundation.
affects:
  - S03
  - S04
key_files:
  - backend/src/RentACar.API/Services/IJwtTokenService.cs
  - backend/src/RentACar.API/Services/JwtTokenService.cs
  - backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs
  - backend/src/RentACar.API/Authentication/RefreshTokenCookieService.cs
  - backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs
  - backend/src/RentACar.API/Controllers/AdminAuthController.cs
  - backend/src/RentACar.API/Options/JwtOptions.cs
  - backend/src/RentACar.API/Options/RefreshTokenCookieSettings.cs
  - backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs
  - backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs
  - backend/tests/RentACar.Tests/Unit/Services/RefreshTokenCookieServiceTests.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs
key_decisions:
  - D008: Shared JWT issuance contract for admin/customer principals via `JwtPrincipalClaims`, with mandatory `sid`/`ver` and 15-minute access-token lifetime.
  - D009: Refresh tokens are 64-byte CSPRNG Base64Url values stored only as `sha256:` hashes and verified with constant-time comparison helpers.
  - D010: JWT bearer auth enforces app-level session + token-version checks after cryptographic validation, with structured unauthorized challenge payloads.
  - D011: Auth conventions are centralized (claim/role constants + refresh-cookie service/settings) and refresh tokens are transported via HttpOnly secure-ready cookie.
patterns_established:
  - Principal-agnostic token issuance pipeline: entity -> shared claim model -> `CreateAccessToken(...)`.
  - Session-authoritative authentication pipeline: JWT signature/lifetime validation -> session lookup (`sid`) -> token-version check (`ver`) -> authorize/deny.
  - Refresh-token transport/storage pattern: issue raw token -> hash at rest -> send only in HttpOnly cookie -> clear cookie on logout.
observability_surfaces:
  - `JwtTokenServiceTests` validate claim contract (`sid`, `ver`, `principal_type`, role/permission), issuer/audience, and 15-minute expiry behavior.
  - `AccessTokenSessionValidatorTests` validate revoked/expired/session-mismatch/version-mismatch rejection paths.
  - `AdminAuthControllerTests` validate structured unauthorized responses and auth-session persistence/cookie interactions.
  - `RefreshTokenCookieServiceTests` validate cookie security flags and deletion semantics.
drill_down_paths:
  - .gsd/milestones/M001/slices/S02/tasks/T1-SUMMARY.md
  - .gsd/milestones/M001/slices/S02/tasks/T2-SUMMARY.md
  - .gsd/milestones/M001/slices/S02/tasks/T3-SUMMARY.md
  - .gsd/milestones/M001/slices/S02/tasks/T4-SUMMARY.md
duration: 3h35m
verification_result: passed
completed_at: 2026-03-15T12:49:04+03:00
---

# S02: JWT & Session Infrastructure

**Shipped end-to-end JWT/session infrastructure primitives (issuance, refresh helpers, DB-backed session validation, and secure cookie conventions) that make session state—not JWT signature alone—the final authority for auth decisions.**

## What Happened

S02 consolidated four tasks into one coherent auth infrastructure layer:

- Generalized access-token issuance for both admin and customer principals through a shared claim contract and enforced 15-minute access-token lifetime.
- Added mandatory diagnostic/session claims (`sid`, `ver`, `principal_type`) and preserved existing admin policy compatibility (`AdminOnly`, `SuperAdminOnly`, admin permission claim).
- Added secure refresh-token primitives: CSPRNG token generation, 7-day expiry semantics, `sha256:` hash-at-rest, constant-time verify helper, and replay predicate for rotation/reuse detection.
- Wired app-level session checks into JWT bearer events so tokens are rejected when backing sessions are revoked/expired/missing or principal token versions no longer match.
- Standardized unauthorized challenge response shape as structured `ApiResponse<object>` with non-leaky messaging.
- Centralized auth conventions (claim/role/permission constants + policy names) and introduced refresh-token cookie service/settings.
- Updated admin login flow to persist `AuthSession` records aligned with token `sid`, store hashed refresh metadata, and set secure-ready HttpOnly refresh cookie; logout now clears cookie.

## Verification

Executed slice-level required verification commands (all passing):

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~JwtTokenServiceTests"` ✅ (15/15)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithValidCredentials_ReturnsOk"` ✅ (1/1)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithInvalidCredentials_ReturnsUnauthorized"` ✅ (1/1)

Additional observability/diagnostic confirmation:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AccessTokenSessionValidatorTests|FullyQualifiedName~RefreshTokenCookieServiceTests|FullyQualifiedName~AuthConventionsTests"` ✅ (15/15)

## Requirements Advanced

- AUTH-03 — Access-token contract/lifetime infrastructure is implemented (15-minute expiry + deterministic claim shape) and verified at service/test level.
- AUTH-04 — Refresh-token lifetime/hash/verification/replay primitives are implemented for 7-day refresh/session rotation flows.
- AUTH-05 — Session/cookie revocation plumbing is now in place (session-aware auth + refresh cookie clear path), enabling reliable logout flows.
- AUTH-07 — Admin login now persists session-backed token context and secure refresh-cookie behavior.
- AUTH-09 — RBAC/auth conventions are now centralized and policy matrix expanded/codified without regressing existing admin policies.

## Requirements Validated

- None (customer/admin full end-to-end auth lifecycle validation remains dependent on S03/S04 API behavior completion).

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

- Task-level `T1-PLAN.md`/`T2-PLAN.md`/`T3-PLAN.md`/`T4-PLAN.md` files were not present; execution followed `S02-PLAN.md` and in-slice task contracts.

## Known Limitations

- Customer-facing register/login/refresh/logout endpoints are not in this slice (planned in S03).
- Admin refresh endpoint behavior is not fully exercised as a complete runtime UAT flow yet; this slice establishes underlying primitives and conventions.

## Follow-ups

- Implement S03 customer auth endpoints to consume session/refresh infrastructure directly.
- Add integration-level refresh-rotation/replay tests once refresh endpoints are exposed.

## Files Created/Modified

- `backend/src/RentACar.API/Authentication/AuthClaimTypes.cs` — canonical auth claim keys.
- `backend/src/RentACar.API/Authentication/AuthPermissionNames.cs` — canonical permission names.
- `backend/src/RentACar.API/Authentication/AuthRoleNames.cs` — canonical role names + admin-role helper.
- `backend/src/RentACar.API/Authentication/AccessTokenSessionValidationFailure.cs` — app-level validation failure reasons.
- `backend/src/RentACar.API/Authentication/IAccessTokenSessionValidator.cs` — validator contract.
- `backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs` — session/token-version auth validator.
- `backend/src/RentACar.API/Authentication/IRefreshTokenCookieService.cs` — refresh-cookie service contract.
- `backend/src/RentACar.API/Authentication/RefreshTokenCookieService.cs` — secure refresh-cookie append/clear behavior.
- `backend/src/RentACar.API/Options/JwtOptions.cs` — access/refresh token lifetime options.
- `backend/src/RentACar.API/Options/RefreshTokenCookieSettings.cs` — refresh-cookie settings model.
- `backend/src/RentACar.API/Services/IJwtTokenService.cs` — shared access-token + refresh helper contract.
- `backend/src/RentACar.API/Services/JwtTokenService.cs` — principal-agnostic JWT issuance and refresh helper implementations.
- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — session persistence + refresh-cookie write/clear wiring.
- `backend/src/RentACar.API/Configuration/AuthPolicyNames.cs` — expanded policy constants.
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — JWT events, validator, cookie service, and auth policy registration.
- `backend/src/RentACar.API/appsettings.json` — JWT and refresh-cookie defaults.
- `backend/src/RentACar.API/appsettings.Development.json` — development JWT/cookie settings.
- `backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs` — token/refresh contract tests.
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs` — session-validation branch coverage.
- `backend/tests/RentACar.Tests/Unit/Services/RefreshTokenCookieServiceTests.cs` — cookie security/deletion tests.
- `backend/tests/RentACar.Tests/Unit/Services/AuthConventionsTests.cs` — role/policy convention tests.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs` — login/logout behavior and failure-shape coverage.

## Forward Intelligence

### What the next slice should know
- S03 should treat access tokens as short-lived session pointers (`sid` + `ver`), not standalone trust artifacts; refresh and logout endpoints must keep DB session state as source of truth.
- Reuse `AuthClaimTypes`/`AuthRoleNames`/`AuthPolicyNames` instead of ad-hoc claim strings to avoid policy drift.

### What's fragile
- Refresh rotation/replay semantics are currently primitive-level; endpoint orchestration mistakes (e.g., wrong active hash handling) could silently weaken replay defense.
- Cookie settings differ by environment (`Always` vs `SameAsRequest` secure policy); local/prod parity tests are important before release.

### Authoritative diagnostics
- `JwtTokenServiceTests` — fastest/high-signal verification for claim contract, expiry, refresh hash/verify/replay behavior.
- `AccessTokenSessionValidatorTests` — canonical branch-level evidence for app-level session acceptance/rejection logic.
- `AdminAuthControllerTests` — best signal for structured auth failure shape and cookie/session wiring at controller boundary.

### What assumptions changed
- “JWT signature + expiry are sufficient for authorization” — changed to “JWT is necessary but not sufficient; active session + token version from DB are mandatory.”
- “Refresh token handling can stay controller-local” — changed to centralized service/settings conventions for safe frontend integration and drift reduction.
