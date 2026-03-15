---
id: T4
parent: S02
milestone: M001
provides:
  - Secure refresh-token cookie conventions are now centralized and applied during admin login/logout via `IRefreshTokenCookieService`.
  - Role/claim conventions are codified for Guest, Customer, Admin, and SuperAdmin across JWT issuance, validation, and authorization policy registration.
key_files:
  - backend/src/RentACar.API/Authentication/RefreshTokenCookieService.cs
  - backend/src/RentACar.API/Options/RefreshTokenCookieSettings.cs
  - backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs
  - backend/src/RentACar.API/Controllers/AdminAuthController.cs
  - backend/src/RentACar.API/Services/JwtTokenService.cs
  - backend/tests/RentACar.Tests/Unit/Services/RefreshTokenCookieServiceTests.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs
key_decisions:
  - Introduced a dedicated refresh-cookie service + settings object instead of ad-hoc cookie writes inside controllers.
  - Centralized auth constants (`AuthClaimTypes`, `AuthRoleNames`, `AuthPermissionNames`) and expanded policy names to include `GuestOnly` and `CustomerOnly` while preserving existing admin policy behavior.
patterns_established:
  - Refresh token transport pattern: issue refresh token -> hash at rest -> set raw token only in HttpOnly secure-ready cookie -> clear same cookie on logout.
  - Auth policy matrix pattern: Guest=unauthenticated (`GuestOnly`), Customer=`CustomerOnly`, Admin=`AdminOnly` (Admin+SuperAdmin), SuperAdmin=`SuperAdminOnly`.
observability_surfaces:
  - `JwtTokenServiceTests` verifies standardized claim keys (`principal_type`, `ver`, role, permission) and `sid`/`ver` session diagnostics contract.
  - `RefreshTokenCookieServiceTests` verifies cookie security flags (`HttpOnly`, `SameSite`, `Secure` policy behavior) and deletion semantics.
  - `AdminAuthControllerTests` verifies cookie write/clear hooks are executed during login/logout.
duration: 1h
verification_result: passed
completed_at: 2026-03-15
blocker_discovered: false
---

# T4: Define cookie and auth pipeline conventions

**Standardized refresh-token cookie delivery and codified shared role/claim/policy conventions across the auth pipeline without regressing existing admin auth behavior.**

## What Happened

Implemented new auth convention primitives:
- Added shared constants for claim keys/permission/roles:
  - `AuthClaimTypes` (`principal_type`, `ver`, `role`, `Permission`)
  - `AuthPermissionNames` (`admin.access`)
  - `AuthRoleNames` (`Guest`, `Customer`, `Admin`, `SuperAdmin` + admin-role helper)
- Added `RefreshTokenCookieSettings` and `IRefreshTokenCookieService` + `RefreshTokenCookieService` to standardize refresh-cookie behavior.

Wired conventions into runtime pipeline:
- `AdminAuthController.Login` now sets refresh token via cookie service (raw token no longer needs to be surfaced anywhere else).
- `AdminAuthController.Logout` now clears the refresh token cookie.
- `ServiceCollectionExtensions` now:
  - configures refresh-cookie settings from `Auth:RefreshTokenCookie`,
  - registers cookie service,
  - codifies authorization matrix with `GuestOnly`, `CustomerOnly`, `AdminOnly`, `SuperAdminOnly`.

Codified role/claim expectations in token handling:
- `JwtTokenService` now validates admin role to be only `Admin` or `SuperAdmin` before token issuance.
- JWT issuance and session validation now consistently use shared claim constants for `principal_type`, `ver`, and `role`.

Configuration updates:
- Added `Auth:RefreshTokenCookie` sections to:
  - `appsettings.json` (secure production-ready defaults, `__Host-` cookie name)
  - `appsettings.Development.json` (`SameAsRequest` secure policy for local HTTP dev ergonomics)

Test coverage updates:
- Added `RefreshTokenCookieServiceTests` for secure cookie attributes and clear/delete behavior.
- Added `AuthConventionsTests` for role/policy constants and admin-role expectations.
- Updated `AdminAuthControllerTests` for cookie write/clear verification.
- Updated `JwtTokenServiceTests` for shared constants and unsupported admin-role rejection.

## Verification

Executed:
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~JwtTokenServiceTests|FullyQualifiedName~AdminAuthControllerTests|FullyQualifiedName~RefreshTokenCookieServiceTests|FullyQualifiedName~AuthConventionsTests"` ✅

Slice-required verification commands (all passed):
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~JwtTokenServiceTests"` ✅
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithValidCredentials_ReturnsOk"` ✅
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithInvalidCredentials_ReturnsUnauthorized"` ✅

## Diagnostics

To inspect this task later:
- Run `RefreshTokenCookieServiceTests` to validate refresh cookie security/deletion conventions.
- Run `AuthConventionsTests` to validate role/policy matrix expectations.
- Run `JwtTokenServiceTests` and decode tokens to confirm claims include `sid`, `ver`, `principal_type`, role claims, and admin permission claim.
- Runtime check: call `POST /api/admin/v1/auth/login` and inspect `Set-Cookie` header for refresh token cookie attributes (`HttpOnly`, `SameSite`, secure policy behavior by environment).

## Deviations

- `.gsd/milestones/M001/slices/S02/tasks/T4-PLAN.md` was not present in the workspace; execution followed `S02-PLAN.md` + carry-forward task summaries as the authoritative contract.

## Known Issues

- None.

## Files Created/Modified

- `backend/src/RentACar.API/Authentication/AuthClaimTypes.cs` — Added canonical claim-key constants used by issuance/validation/controllers.
- `backend/src/RentACar.API/Authentication/AuthPermissionNames.cs` — Added admin permission claim constant.
- `backend/src/RentACar.API/Authentication/AuthRoleNames.cs` — Added role constants and admin-role helper.
- `backend/src/RentACar.API/Options/RefreshTokenCookieSettings.cs` — Added configurable refresh-cookie convention settings.
- `backend/src/RentACar.API/Authentication/IRefreshTokenCookieService.cs` — Added cookie service contract.
- `backend/src/RentACar.API/Authentication/RefreshTokenCookieService.cs` — Implemented secure refresh-cookie append/clear behavior.
- `backend/src/RentACar.API/Configuration/AuthPolicyNames.cs` — Added `GuestOnly` and `CustomerOnly` policy constants.
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — Registered cookie service/settings and codified policy matrix.
- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — Wired refresh cookie append on login and clear on logout.
- `backend/src/RentACar.API/Controllers/AdminSecurityController.cs` — Switched role-claim read to shared constant.
- `backend/src/RentACar.API/Services/JwtTokenService.cs` — Reused shared auth constants and enforced admin role expectation.
- `backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs` — Reused shared claim constants for claim parsing.
- `backend/src/RentACar.API/appsettings.json` — Added production refresh-cookie config defaults.
- `backend/src/RentACar.API/appsettings.Development.json` — Added development refresh-cookie config defaults.
- `backend/tests/RentACar.Tests/Unit/Services/RefreshTokenCookieServiceTests.cs` — Added cookie convention coverage.
- `backend/tests/RentACar.Tests/Unit/Services/AuthConventionsTests.cs` — Added role/policy convention coverage.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs` — Added refresh-cookie service interaction assertions.
- `backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs` — Updated claims assertions and added unsupported admin-role test.
- `.gsd/milestones/M001/slices/S02/S02-PLAN.md` — Marked T4 complete.
- `.gsd/DECISIONS.md` — Appended D011 decision for centralized auth/cookie conventions.
- `.gsd/STATE.md` — Updated decisions and next action after completing S02/T4.
