---
id: T03
parent: S03
milestone: M001
provides:
  - Customer refresh/logout lifecycle completion with DB-backed refresh rotation, replay-safe rejection, and logout session revocation.
key_files:
  - backend/src/RentACar.API/Controllers/CustomerAuthController.cs
  - backend/src/RentACar.API/Contracts/Auth/CustomerRefreshResponse.cs
  - backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs
  - backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs
  - backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs
key_decisions:
  - D013: Customer refresh uses session replacement semantics (`revoked_at_utc` + `replaced_by_session_id`) instead of cookie-only overwrite.
  - D016: Access-token session validation treats `replaced_by_session_id` as revoked-equivalent.
patterns_established:
  - Refresh flow reads refresh cookie by configured cookie name, hashes token, and resolves only active non-revoked/non-replaced customer sessions.
  - Successful refresh revokes prior session, links lineage via `replaced_by_session_id`, persists replacement session, and rotates refresh cookie.
  - Logout revokes current session via access-token `sid` + principal context and clears refresh cookie in the same flow.
observability_surfaces:
  - `CustomerAuthControllerTests` assertions over `auth_sessions.revoked_at_utc` and `auth_sessions.replaced_by_session_id` before/after refresh/logout.
  - `CustomerAuthControllerTests` replay/revoked/expired unauthorized contract checks (`ApiResponse<object>`, generic message).
  - `AccessTokenSessionValidatorTests` explicit replaced-session rejection assertion (`SessionRevoked`).
  - Refresh-cookie rotation/clear behavior verified through `IRefreshTokenCookieService` interaction assertions.
duration: 1h25m
verification_result: passed
completed_at: 2026-03-15
blocker_discovered: false
---

# T03: Add customer refresh/logout session rotation and lifecycle verification

**Implemented customer refresh rotation and logout revocation endpoints with server-side session lifecycle enforcement, plus test coverage for replay/revoked/expired failure branches and replaced-session validator behavior.**

## What Happened

Implemented `POST /api/customer/v1/auth/refresh` in `CustomerAuthController`:
- Reads configured refresh-cookie name via `RefreshTokenCookieSettings`.
- Hashes incoming refresh token and resolves matching customer `AuthSession` by hashed token.
- Rejects missing/revoked/replaced/expired session paths with generic unauthorized payload.
- Validates principal state by requiring existing credentialed customer.
- Rotates session state on success by setting prior session `revoked_at_utc` and `replaced_by_session_id`, creating a replacement session row, issuing new access+refresh tokens, and writing a rotated refresh cookie.
- Returns typed refresh contract `CustomerRefreshResponse`.

Implemented `POST /api/customer/v1/auth/logout`:
- Uses authenticated principal claims (`sub`/`nameidentifier` + `sid`) to identify the current customer session.
- Revokes the matching server-side session (`revoked_at_utc`) and updates last-seen marker.
- Clears refresh cookie in the same request flow.

Extended session validator behavior:
- `AccessTokenSessionValidator` now treats `ReplacedBySessionId` as revoked-equivalent so rotated-out sessions are denied even if only replacement lineage is present.

Expanded tests:
- `CustomerAuthControllerTests`
  - `Refresh_WithActiveSession_RotatesSessionAndIssuesNewTokens`
  - `Refresh_WithReplayedRefreshToken_ReturnsUnauthorizedGenericPayload`
  - `Refresh_WithExpiredSession_ReturnsUnauthorizedGenericPayload`
  - `Logout_WithValidSession_RevokesSessionAndClearsRefreshCookie`
- `AccessTokenSessionValidatorTests`
  - `ValidateAsync_WhenSessionIsReplaced_ReturnsSessionRevoked`
- `JwtTokenServiceTests`
  - `IsRefreshTokenReplay_WhenUsingActiveToken_ReturnsFalse`

Also appended decision `D016` to `.gsd/DECISIONS.md`.

## Verification

Task-level verification commands (from T03 plan):
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~CustomerAuthControllerTests"` ✅ (13/13 passed)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AccessTokenSessionValidatorTests|FullyQualifiedName~JwtTokenServiceTests"` ✅ (23/23 passed)

Slice-level verification commands (from S03 plan):
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests|FullyQualifiedName~ReservationServiceTests"` ✅ (28/28 passed)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~CustomerAuthControllerTests"` ✅ (13/13 passed)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AccessTokenSessionValidatorTests"` ✅ (7/7 passed)

Failure-path observability verification:
- Replay/revoked/expired refresh flows assert deterministic `ApiResponse<object>` unauthorized outputs.
- Refresh success/logout tests assert persisted session transition markers (`revoked_at_utc`, `replaced_by_session_id`) and cookie lifecycle interactions.

## Diagnostics

Future agents can inspect:
- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs` for refresh/logout branching, rotation semantics, and claim extraction logic.
- `backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs` for deterministic lifecycle assertions across success and failure paths.
- `backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs` + `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs` for replaced-session rejection behavior.
- `auth_sessions` rows to inspect lineage (`replaced_by_session_id`) and revocation timestamps around refresh/logout flows.

## Deviations

- None.

## Known Issues

- None.

## Files Created/Modified

- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs` — Added refresh/logout endpoints with DB-backed session rotation/revocation lifecycle.
- `backend/src/RentACar.API/Contracts/Auth/CustomerRefreshResponse.cs` — Added refresh response contract.
- `backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs` — Treats replaced sessions as revoked.
- `backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs` — Added refresh/logout success/failure coverage and session-transition assertions.
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs` — Added replaced-session rejection test.
- `backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs` — Added non-replay helper contract test.
- `.gsd/DECISIONS.md` — Appended D016.
- `.gsd/milestones/M001/slices/S03/S03-PLAN.md` — Marked T03 complete.
- `.gsd/STATE.md` — Updated current execution state after completing T03.
