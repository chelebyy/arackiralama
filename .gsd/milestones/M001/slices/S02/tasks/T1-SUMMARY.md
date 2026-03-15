---
id: T1
parent: S02
milestone: M001
provides:
  - Shared JWT issuance infrastructure for admin and customer principals with `sid`/`ver` claim contract and 15-minute access-token lifetime
key_files:
  - backend/src/RentACar.API/Services/IJwtTokenService.cs
  - backend/src/RentACar.API/Services/JwtTokenService.cs
  - backend/src/RentACar.API/Authentication/JwtPrincipalClaims.cs
  - backend/src/RentACar.API/Options/JwtOptions.cs
  - backend/src/RentACar.API/Controllers/AdminAuthController.cs
  - backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs
  - backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs
  - .gsd/milestones/M001/slices/S02/S02-PLAN.md
  - .gsd/DECISIONS.md
  - .gsd/STATE.md
key_decisions:
  - D008: use shared `JwtPrincipalClaims` model + mandatory `sid`/`ver` claims for both principal types, with 15-minute access-token lifetime
patterns_established:
  - Token issuance for any principal type should map entity -> `JwtPrincipalClaims` -> shared `CreateAccessToken(...)` flow (avoid duplicated per-role token assembly)
observability_surfaces:
  - JWT payload decoding exposes `sid`, `ver`, `principal_type`, role claims, issuer/audience, and expiry window for diagnostics
  - Unauthorized admin-login responses remain structured (`ApiResponse<object>`) for failure-path inspection
  - `JwtTokenServiceTests` and targeted `AdminAuthControllerTests` provide repeatable diagnostics for token contract and failure behavior
duration: 52m
verification_result: passed
completed_at: 2026-03-15T12:19:45+03:00
# Set blocker_discovered: true only if execution revealed the remaining slice plan
# is fundamentally invalid (wrong API, missing capability, architectural mismatch).
# Do NOT set true for ordinary bugs, minor deviations, or fixable issues.
blocker_discovered: false
---

# T1: Generalize JWT issuance for both principal types

**Refactored JWT issuance into a shared principal-claims pipeline for admin/customer tokens, enforced 15-minute access-token lifetime, and added `sid` + `ver` claims to every issued access token.**

## What Happened

Implemented T1 by replacing admin-only JWT construction with a shared claim model:

- Added `JwtPrincipalClaims` (`backend/src/RentACar.API/Authentication/JwtPrincipalClaims.cs`) as the common token input model.
- Expanded `IJwtTokenService` to support both principal types:
  - `CreateAdminAccessToken(AdminUser, Guid sessionId, out DateTime expiresAtUtc)`
  - `CreateCustomerAccessToken(Customer, Guid sessionId, out DateTime expiresAtUtc)`
- Refactored `JwtTokenService` to:
  - map both principal types into the shared model
  - emit mandatory `sid` and `ver` claims
  - emit `principal_type` claim (`Admin` / `Customer`)
  - preserve admin role/policy compatibility (`ClaimTypes.Role` + `role`, and `Permission=admin.access` for admins)
  - enforce 15-minute token lifetime via new `JwtOptions.AccessTokenMinutes`
  - enforce non-empty session id and secret/issuer/audience validation
- Added `JwtOptions` and wired options binding in `ServiceCollectionExtensions`.
- Updated appsettings to `Jwt:AccessTokenMinutes = 15` (removed hours-based setting).
- Updated `AdminAuthController` login flow to pass a generated session id into JWT issuance (preparing for T2/T3 session persistence/validation).
- Updated unit tests:
  - rewrote `JwtTokenServiceTests` for admin/customer shared claim contract, `sid`/`ver`, and 15-minute expiry
  - updated/renamed admin login success test to `Login_WithValidCredentials_ReturnsOk`
  - added explicit failure-path diagnostic test `Login_WithInvalidCredentials_ReturnsUnauthorized`

Pre-flight requirement was also handled first: `S02-PLAN.md` now includes `## Observability / Diagnostics` and an explicit failure-path verification command.

## Verification

Slice verification commands (from `S02-PLAN.md`) were executed and passed:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~JwtTokenServiceTests"`  
  Result: **Passed** (9/9)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithValidCredentials_ReturnsOk"`  
  Result: **Passed** (1/1)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithInvalidCredentials_ReturnsUnauthorized"`  
  Result: **Passed** (1/1)

## Diagnostics

How to inspect T1 outputs later:

- Decode an issued JWT and verify claim contract:
  - required: `sid`, `ver`, `principal_type`, `sub`, `email`
  - admin compatibility: `ClaimTypes.Role` / `role`, `Permission=admin.access`
- Run `JwtTokenServiceTests` for deterministic validation of claim shape and expiry policy.
- Run `AdminAuthControllerTests.Login_WithInvalidCredentials_ReturnsUnauthorized` to confirm structured unauthorized failure surface (`ApiResponse<object>`).

Redaction constraints preserved: no plaintext secret/token/hash logging was introduced.

## Deviations

- The dispatch-referenced task plan file (`.gsd/milestones/M001/slices/S02/tasks/T1-PLAN.md`) is missing in the workspace, so execution followed the slice contract in `S02-PLAN.md` plus dispatch instructions.

## Known Issues

- None for T1 scope.

## Files Created/Modified

- `backend/src/RentACar.API/Options/JwtOptions.cs` — introduced JWT options model (`AccessTokenMinutes`, issuer/audience/secret).
- `backend/src/RentACar.API/Authentication/JwtPrincipalClaims.cs` — introduced shared claim model for principal-agnostic token issuance.
- `backend/src/RentACar.API/Services/IJwtTokenService.cs` — expanded service contract for admin+customer token issuance with session id input.
- `backend/src/RentACar.API/Services/JwtTokenService.cs` — refactored to shared issuance pipeline, `sid`/`ver` claims, and 15-minute expiry.
- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — login now issues admin tokens with generated session id.
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — bound `JwtOptions` and updated JWT auth setup to consume options.
- `backend/src/RentACar.API/appsettings.json` — switched JWT lifetime setting to `AccessTokenMinutes: 15`.
- `backend/src/RentACar.API/appsettings.Development.json` — switched JWT lifetime setting to `AccessTokenMinutes: 15`.
- `backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs` — updated coverage for shared principal model and claims contract.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs` — updated for new signature and added invalid-credentials diagnostic check.
- `.gsd/milestones/M001/slices/S02/S02-PLAN.md` — added observability/verification sections (pre-flight) and marked T1 complete.
- `.gsd/DECISIONS.md` — appended D008 JWT contract decision.
- `.gsd/STATE.md` — refreshed active execution state after T1.
