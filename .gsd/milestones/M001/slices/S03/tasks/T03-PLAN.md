---
estimated_steps: 5
estimated_files: 5
---

# T03: Add customer refresh/logout revocation flow and lifecycle verification

**Slice:** S03 — Customer Auth API
**Milestone:** M001

## Description

Complete the customer auth lifecycle by implementing refresh-token rotation and logout revocation using DB-backed session truth. This task closes AUTH-04 and AUTH-05 with replay-aware session transitions and verification coverage for failure paths.

## Steps

1. Implement **refresh** endpoint to read refresh cookie, locate matching hashed session, validate active session/principal state, and reject replay/revoked/expired cases.
2. Apply rotation semantics on successful refresh by revoking/replacing prior session state, issuing new access+refresh tokens, persisting new session data, and writing new refresh cookie.
3. Implement **logout** endpoint to revoke current session (`sid` + principal context) and clear refresh cookie.
4. Extend customer auth controller tests for refresh success, replay attempt rejection, revoked/expired session rejection, and logout revocation behavior.
5. Add/extend validator/token tests where needed to ensure revoked/replaced sessions are rejected consistently by session validator logic.

## Must-Haves

- [ ] Refresh endpoint accepts only active, non-revoked customer sessions within refresh lifetime.
- [ ] Refresh rotation updates session state so old refresh artifacts cannot be reused silently.
- [ ] Logout revokes server-side session state and clears refresh cookie in the same flow.
- [ ] Failure responses remain generic `ApiResponse<object>` unauthorized responses with no token leakage.

## Verification

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~CustomerAuthControllerTests"`
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AccessTokenSessionValidatorTests|FullyQualifiedName~JwtTokenServiceTests"`

## Observability Impact

- Signals added/changed: `auth_sessions.revoked_at_utc`, `auth_sessions.replaced_by_session_id`, and refresh-cookie rotation events.
- How a future agent inspects this: controller/validator tests plus DB session rows for pre/post refresh/logout transitions.
- Failure state exposed: replay/revoked/expired refresh paths are test-addressable and map to deterministic unauthorized outcomes.

## Inputs

- `.gsd/milestones/M001/slices/S03/tasks/T02-PLAN.md` — customer register/login session creation behavior.
- `backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs` — session truth enforcement for access tokens.
- `backend/src/RentACar.API/Authentication/RefreshTokenCookieService.cs` — cookie write/clear conventions.
- `backend/src/RentACar.API/Services/JwtTokenService.cs` — refresh token hash/verify/replay helpers.

## Expected Output

- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs` — refresh/logout lifecycle endpoints.
- `backend/src/RentACar.API/Contracts/Auth/CustomerRefreshResponse.cs` — refresh response contract.
- `backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs` — refresh/logout coverage.
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs` — revoked/replaced-session rejection assertions.
- `backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs` — refresh replay helper contract coverage updates (if needed).
