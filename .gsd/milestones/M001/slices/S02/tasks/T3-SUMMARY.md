---
id: T3
parent: S02
milestone: M001
provides:
  - JWT bearer authentication now enforces application-level session and token-version validation after signature/lifetime checks.
  - Admin login now persists an `AuthSession` row (with hashed refresh token metadata) so newly issued access tokens reference a real session id.
key_files:
  - backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs
  - backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs
  - backend/src/RentACar.API/Controllers/AdminAuthController.cs
  - backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs
key_decisions:
  - Added a dedicated `IAccessTokenSessionValidator` service to isolate claim parsing + DB-backed session/version checks from JWT event wiring.
  - Standardized unauthorized challenge payloads for bearer auth failures as `ApiResponse<object>.Fail("Yetkisiz erişim")` to keep failure surfaces structured and non-leaky.
patterns_established:
  - Access token acceptance now follows: cryptographic JWT validation -> app-level session lookup (`sid`) -> principal token-version match (`ver`) against current DB state.
observability_surfaces:
  - Structured warning logs from `AccessTokenSessionValidator` include failure reason plus principal/session/version identifiers (no token/hash/secret material).
  - Unauthorized bearer challenges return a consistent JSON `ApiResponse<object>` body.
duration: 1h
verification_result: passed
completed_at: 2026-03-15
blocker_discovered: false
---

# T3: Wire application-level session validation into auth pipeline

**Shipped DB-backed session validation in JWT bearer auth and made admin login persist auth sessions compatible with `sid`/`ver` token contracts.**

## What Happened

Implemented a new authentication validator (`AccessTokenSessionValidator`) that reads `principal_type`, `sub`, `sid`, and `ver` claims from validated access tokens and then enforces runtime session state from the database:
- rejects missing/invalid claim contracts,
- rejects missing/mismatched sessions,
- rejects revoked or expired sessions,
- rejects stale tokens when principal `TokenVersion` differs from token `ver`.

Wired this validator into `AddJwtBearer(...).Events.OnTokenValidated` so app-level session checks run after JWT signature/lifetime verification. Also added `OnChallenge` handling to emit structured unauthorized payloads (`ApiResponse<object>`) instead of opaque default challenge responses.

Updated `AdminAuthController.Login` to persist `AuthSession` during successful login using:
- generated `sessionId` (bound to access token `sid`),
- generated refresh token expiry metadata,
- hashed refresh token (`sha256:`) storage only,
- request metadata (`CreatedByIp`, `UserAgent`) when available.

Expanded tests:
- new `AccessTokenSessionValidatorTests` covering success + revoked/expired/version-mismatch/missing-claim/session-mismatch branches,
- updated `AdminAuthControllerTests.Login_WithValidCredentials_ReturnsOk` to verify auth-session persistence and refresh-hash flow.

## Verification

Executed:
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AccessTokenSessionValidatorTests|FullyQualifiedName~AdminAuthControllerTests.Login_WithValidCredentials_ReturnsOk"` ✅
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~JwtTokenServiceTests"` ✅
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithValidCredentials_ReturnsOk"` ✅
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithInvalidCredentials_ReturnsUnauthorized"` ✅

## Diagnostics

To inspect this task later:
- Run `AccessTokenSessionValidatorTests` for branch-level session-validation behavior.
- For runtime auth failures, inspect warning logs from `AccessTokenSessionValidator` (`reason`, `principal_type`, `principal_id`, `session_id`, `token_version`).
- For client-visible failure shape, call any `[Authorize]` endpoint with an invalid/missing bearer token and verify 401 body is `ApiResponse<object>` with message `Yetkisiz erişim`.

## Deviations

- `.gsd/milestones/M001/slices/S02/tasks/T3-PLAN.md` was not present in the workspace; execution followed the slice contract in `S02-PLAN.md` plus carry-forward summaries.

## Known Issues

- None.

## Files Created/Modified

- `backend/src/RentACar.API/Authentication/AccessTokenSessionValidationFailure.cs` — Added explicit failure reason enum for app-level token/session checks.
- `backend/src/RentACar.API/Authentication/IAccessTokenSessionValidator.cs` — Added validator interface for JWT event integration.
- `backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs` — Implemented claim parsing, session lookup, revocation/expiry checks, token-version enforcement, and redaction-safe logging.
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — Registered validator, wired `OnTokenValidated` session checks, and standardized unauthorized challenge payloads.
- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — Persisted auth sessions during successful admin login with hashed refresh token metadata.
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs` — Added unit coverage for session-validation success/failure paths.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs` — Updated valid-login test to verify auth-session persistence and refresh-token helper usage.
- `.gsd/milestones/M001/slices/S02/S02-PLAN.md` — Marked T3 complete.
- `.gsd/DECISIONS.md` — Appended D010 decision about app-level session validation and structured unauthorized responses.
- `.gsd/STATE.md` — Updated recent decision and advanced next action to T4.
