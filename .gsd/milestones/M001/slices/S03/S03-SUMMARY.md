---
id: S03
parent: M001
milestone: M001
provides:
  - Customer register/login/me/refresh/logout API with DB-backed session lifecycle, lockout enforcement, and non-enumerating unauthorized contracts
requires:
  - slice: S02
    provides: JWT/session primitives (15m access token, refresh hashing/cookies, bearer session validation)
affects:
  - S04
  - S05
key_files:
  - backend/src/RentACar.API/Controllers/CustomerAuthController.cs
  - backend/src/RentACar.API/Contracts/Auth/CustomerRegisterRequest.cs
  - backend/src/RentACar.API/Contracts/Auth/CustomerLoginRequest.cs
  - backend/src/RentACar.API/Contracts/Auth/CustomerAuthResponse.cs
  - backend/src/RentACar.API/Contracts/Auth/CustomerRefreshResponse.cs
  - backend/src/RentACar.Core/Entities/Customer.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs
  - backend/src/RentACar.API/Services/ReservationService.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs
  - backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs
key_decisions:
  - D013: Customer refresh rotation uses session replacement lineage (`revoked_at_utc` + `replaced_by_session_id`).
  - D014: Customer credential storage remains nullable and reservation-customer matching must use normalized email.
  - D015: Register/login failures remain non-enumerating while lockout mutations remain observable in persisted state.
  - D016: Session validator treats `replaced_by_session_id` as revoked-equivalent.
patterns_established:
  - Customer identity resolution in auth-adjacent flows queries `NormalizedEmail` only.
  - Reservation-created customer rows are upgraded in-place at registration when no credential exists.
  - Login lockout policy is deterministic (5 failed attempts => 15-minute lockout) with reset on successful authentication.
  - Refresh requires an active non-revoked/non-replaced/non-expired session and rotates both DB session state and refresh cookie.
  - Logout revokes server-side session by authenticated `sid` context and clears refresh cookie.
observability_surfaces:
  - `customers.failed_login_count`, `customers.lockout_end_utc`, `customers.last_login_at_utc`, `customers.token_version`
  - `auth_sessions.revoked_at_utc`, `auth_sessions.replaced_by_session_id`
  - Generic unauthorized `ApiResponse<object>` assertions in customer auth tests (invalid credentials/lockout/replay/expired branches)
  - `AccessTokenSessionValidatorTests.ValidateAsync_WhenSessionIsReplaced_ReturnsSessionRevoked`
drill_down_paths:
  - .gsd/milestones/M001/slices/S03/tasks/T01-SUMMARY.md
  - .gsd/milestones/M001/slices/S03/tasks/T02-SUMMARY.md
  - .gsd/milestones/M001/slices/S03/tasks/T03-SUMMARY.md
duration: 4h05m
verification_result: passed
completed_at: 2026-03-15
---

# S03: Customer Auth API

**Shipped customer auth endpoints (register/login/me/refresh/logout) on top of S02 JWT/session primitives with lockout, refresh rotation, replay-safe rejection, and server-side logout revocation.**

## What Happened

S03 closed the customer-auth slice end to end in three ordered tasks:

- **T01 (credential substrate):** made `Customer` credential-ready via nullable `password_hash`, hardened reservation customer reuse by normalized-email lookup, and locked both invariants with domain/service tests + migration artifacts.
- **T02 (entry endpoints):** implemented `CustomerAuthController` register/login/me contracts under `api/customer/v1/auth`, including guest-row upgrade semantics, generic unauthorized failures, 5-attempt/15-minute lockout behavior, and successful customer session issuance with refresh cookie append.
- **T03 (lifecycle closure):** implemented refresh and logout with DB-backed session rotation/revocation semantics; replay/revoked/expired refresh branches reject generically; replaced sessions are denied by access-token validator.

Result: customer authentication now exercises the full session lifecycle in persistence-backed state instead of token/cookie-only behavior.

## Verification

Executed all slice-level verification commands from `S03-PLAN.md`:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests|FullyQualifiedName~ReservationServiceTests"` ✅ (28 passed)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~CustomerAuthControllerTests"` ✅ (13 passed)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AccessTokenSessionValidatorTests"` ✅ (7 passed)

Observability/diagnostic branches were also re-confirmed with targeted tests:

- `dotnet test ... --filter "FullyQualifiedName~Refresh_WithReplayedRefreshToken|FullyQualifiedName~Refresh_WithExpiredSession|FullyQualifiedName~Logout_WithValidSession|FullyQualifiedName~ValidateAsync_WhenSessionIsReplaced"` ✅ (4 passed)

## Requirements Advanced

- AUTH-09 — customer-authenticated policy surfaces are now exercised (`CustomerOnly` on `me` + customer-principal session claims); full cross-role RBAC remains pending S04.

## Requirements Validated

- AUTH-01 — customer registration with email/password is implemented and tested.
- AUTH-02 — customer login with email/password is implemented and tested.
- AUTH-03 — issued customer access JWTs use the shared 15-minute contract and pass controller/session tests.
- AUTH-04 — refresh within 7-day session/token lifetime is implemented with active-session checks and rotation.
- AUTH-05 — logout performs server-side session revocation and cookie clear.
- AUTH-10 — account lockout after 5 failed attempts is implemented and asserted.

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

None.

## Known Limitations

- AUTH-06 (password reset via email link) is still not implemented.
- Admin authentication + full RBAC closure (AUTH-07/AUTH-08 and full AUTH-09) remains in S04.
- Frontend auth integration remains in S05.

## Follow-ups

- Start S04 by introducing admin auth endpoints using the same session/token lifecycle semantics.
- Extend RBAC verification to include admin/superadmin policy matrices once S04 endpoints land.

## Files Created/Modified

- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs` — register/login/me/refresh/logout behaviors.
- `backend/src/RentACar.API/Contracts/Auth/CustomerRegisterRequest.cs` — register contract.
- `backend/src/RentACar.API/Contracts/Auth/CustomerLoginRequest.cs` — login contract.
- `backend/src/RentACar.API/Contracts/Auth/CustomerAuthResponse.cs` — register/login success payload.
- `backend/src/RentACar.API/Contracts/Auth/CustomerRefreshResponse.cs` — refresh success payload.
- `backend/src/RentACar.Core/Entities/Customer.cs` — nullable credential fields + auth state semantics.
- `backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs` — `password_hash` mapping.
- `backend/src/RentACar.API/Services/ReservationService.cs` — normalized-email customer reuse.
- `backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs` — endpoint + lifecycle assertions.
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs` — replaced-session rejection.
- `backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs` — refresh replay helper contract coverage.

## Forward Intelligence

### What the next slice should know
- Admin auth should reuse the same session lineage model (`revoked_at_utc` + `replaced_by_session_id`) to keep refresh/replay diagnostics consistent between principal types.
- Keep unauthorized payloads generic (`ApiResponse<object>`) across all auth failures; tests now depend on this contract.

### What's fragile
- Claim extraction for logout (`sid` + principal id) is sensitive to missing claims; any claim naming drift will silently break revocation targeting.
- Refresh flow correctness depends on hash-at-rest lookup and replacement linkage; skipping either reintroduces replay acceptance risk.

### Authoritative diagnostics
- `CustomerAuthControllerTests` is the fastest truth source for lockout/replay/logout lifecycle behavior.
- `AccessTokenSessionValidatorTests.ValidateAsync_WhenSessionIsReplaced_ReturnsSessionRevoked` is the canonical guard for rotated-session denial.

### What assumptions changed
- Original assumption: reservation-created customers could remain separate from credentialed users by raw-email comparisons.
- Actual behavior: normalized-email identity unification is mandatory to prevent case-drift account forking and to allow safe guest-row credential upgrades.
