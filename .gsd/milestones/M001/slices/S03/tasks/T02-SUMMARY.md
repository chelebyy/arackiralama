---
id: T02
parent: S03
milestone: M001
provides:
  - Customer register/login/me API surface with normalized-email identity, guest-account upgrade, lockout state transitions, and customer session issuance.
key_files:
  - backend/src/RentACar.API/Controllers/CustomerAuthController.cs
  - backend/src/RentACar.API/Contracts/Auth/CustomerRegisterRequest.cs
  - backend/src/RentACar.API/Contracts/Auth/CustomerLoginRequest.cs
  - backend/src/RentACar.API/Contracts/Auth/CustomerAuthResponse.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs
  - .gsd/DECISIONS.md
key_decisions:
  - Customer registration upgrades an existing reservation-created customer when normalized email matches and no password exists; already-credentialed rows are rejected with generic unauthorized payload.
  - Customer login uses 5 failed attempts / 15-minute lockout, resets counters on successful authentication, and emits session-backed access+refresh credentials.
  - Login/register failure payloads stay non-enumerating (`ApiResponse<object>` + generic unauthorized) for account-existence-safe behavior.
patterns_established:
  - Customer auth identity lookups must query `Customer.NormalizedEmail` (never raw `Email`).
  - Lockout-related observability (`failed_login_count`, `lockout_end_utc`, `last_login_at_utc`) is asserted through controller tests, not inferred.
  - Successful customer login persists `AuthSession` rows (`PrincipalType=Customer`) and appends refresh cookie via `IRefreshTokenCookieService`.
observability_surfaces:
  - `CustomerAuthControllerTests` assertions on `customers.failed_login_count`, `customers.lockout_end_utc`, `customers.last_login_at_utc`.
  - `CustomerAuthControllerTests` assertions on `auth_sessions` insert state after successful login.
  - Unauthorized error contract inspection through `ApiResponse<object>` assertions in register/login failure tests.
duration: 1h40m
verification_result: passed
completed_at: 2026-03-15
blocker_discovered: false
---

# T02: Implement customer register/login/me endpoints with lockout policy

**Added customer auth API endpoints (`register`, `login`, `me`) with normalized-email identity handling, guest-record upgrade, lockout enforcement, and session-backed login token issuance.**

## What Happened

Implemented a new `CustomerAuthController` under `api/customer/v1/auth` and introduced customer auth contracts (`CustomerRegisterRequest`, `CustomerLoginRequest`, `CustomerAuthResponse`).

Shipped register behavior with normalized-email lookup and guest-upgrade semantics:
- New account path: creates `Customer` with hashed password.
- Existing guest path (`password_hash == null`): upgrades same row by assigning hashed password and optional profile updates.
- Existing credentialed row: rejected with generic unauthorized response to avoid account existence leakage.

Shipped login behavior with deterministic lockout/session transitions:
- Generic unauthorized for missing credentials / unknown customer / missing password / invalid password / locked account.
- Failed password increments `FailedLoginCount`; threshold `>= 5` sets `LockoutEndUtc = now + 15m`.
- Successful login clears lockout counters, sets `LastLoginAtUtc`, persists `AuthSession` (`PrincipalType=Customer`), issues customer access token + refresh token, and sets refresh cookie.

Shipped `me` endpoint secured with `CustomerOnly` policy and standard rate limit policy, returning current principal identity claims in API response envelope.

Added `CustomerAuthControllerTests` covering register/login/me success and failure branches, including lockout and non-enumerating unauthorized payload checks.

## Verification

Executed and passed:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~CustomerAuthControllerTests"`
  - Result: **9/9 passed**
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests"`
  - Result: **6/6 passed**

Slice-level checks executed and passed during this task:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests|FullyQualifiedName~ReservationServiceTests"`
  - Result: **28/28 passed**
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AccessTokenSessionValidatorTests"`
  - Result: **6/6 passed**
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~CustomerAuthControllerTests"`
  - Result: **9/9 passed**

## Diagnostics

Future inspection surfaces:

- `backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs`
  - `Login_WithInvalidPassword_TracksFailuresAndLocksOnFifthAttempt`
  - `Login_WithValidCredentials_ResetsLockoutAndPersistsCustomerSession`
  - `Login_WhenCustomerHasNoPassword_ReturnsUnauthorizedWithoutMutation`
  - `Register_WhenGuestCustomerExists_UpgradesExistingCustomer`
  - `Register_WhenCustomerAlreadyRegistered_ReturnsUnauthorizedWithoutLeaking`
- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs`
  - Lockout constants and transitions, normalized-email lookup path, and session creation flow.

These cover runtime signals listed in the task plan (`failed_login_count`, `lockout_end_utc`, `last_login_at_utc`, `auth_sessions` creation) and confirm generic unauthorized contract behavior.

## Deviations

- No structural deviations from T02 plan.
- `ServiceCollectionExtensions.cs` required no wiring changes because `CustomerOnly` policy, JWT/session validator, and refresh-cookie services were already present from prior slices.

## Known Issues

- Refresh/logout lifecycle (rotation/revocation) is intentionally not implemented in T02 and remains for T03.

## Files Created/Modified

- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs` — Added customer register/login/me endpoints with normalized lookup, lockout policy, and session issuance.
- `backend/src/RentACar.API/Contracts/Auth/CustomerRegisterRequest.cs` — Added register request contract.
- `backend/src/RentACar.API/Contracts/Auth/CustomerLoginRequest.cs` — Added login request contract.
- `backend/src/RentACar.API/Contracts/Auth/CustomerAuthResponse.cs` — Added login success payload contract.
- `backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs` — Added controller test coverage for register/login/me and lockout/non-enumeration branches.
- `.gsd/milestones/M001/slices/S03/S03-PLAN.md` — Marked T02 complete.
- `.gsd/DECISIONS.md` — Appended D015 for non-enumerating failure contract + lockout mutation policy.
