# S03: Customer Auth API

**Goal:** register/login/refresh/logout endpoints for customers using S02 session-auth primitives
**Demo:** a customer can register, login, refresh access, and logout while lockout/revocation rules are enforced by DB-backed session state

## Decomposition Rationale

I am grouping S03 into three tasks because the highest risk is ordering-dependent:

1. **Credential substrate first (T01)** — S03 cannot satisfy AUTH-01/AUTH-02 until `Customer` is credential-capable and duplicate-customer drift from reservation-created rows is controlled.
2. **Register/login next (T02)** — once persistence is safe, we can implement the primary customer entrypoints and lockout behavior (AUTH-10) using existing S02 token/session/cookie primitives.
3. **Refresh/logout and lifecycle closure last (T03)** — refresh rotation/replay and logout revocation are the most stateful paths; they should be implemented after login writes correct session records.

Verification emphasizes **controller contract tests** and **state assertions** (customer lockout/session rows/revocation markers) so future agents can diagnose failures without guessing from JWTs alone.

## Must-Haves

- Customer registration supports email/password and safely handles pre-existing reservation-created customer rows.
- Customer login issues 15-minute access tokens via S02 token service and writes refresh-cookie/session state.
- Failed logins lock the account after 5 attempts, with deterministic reset/unlock behavior on successful authentication.
- Customer refresh works within 7-day refresh lifetime and applies rotation/replay-safe session transitions.
- Customer logout revokes server-side session state (not cookie-only) and clears refresh cookie.
- Customer auth endpoints remain non-enumerating on unauthorized paths and use strict rate limiting.

## Requirement Coverage

- **Owned by S03:** AUTH-01, AUTH-02, AUTH-04, AUTH-05, AUTH-10
- **Directly supported by S03:** AUTH-03 (consumes 15-minute token contract), AUTH-09 (customer role/policy enforcement on customer-authenticated surfaces)

Task mapping:

- **T01** → AUTH-01 (credential persistence precondition), AUTH-02 (login lookup correctness)
- **T02** → AUTH-01, AUTH-02, AUTH-03, AUTH-10, AUTH-09(partial)
- **T03** → AUTH-04, AUTH-05, AUTH-03, AUTH-09(partial)

## Proof Level

- This slice proves: integration
- Real runtime required: yes
- Human/UAT required: no

## Verification

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests|FullyQualifiedName~ReservationServiceTests"`
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~CustomerAuthControllerTests"`
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AccessTokenSessionValidatorTests"`
- Verify failure-path observability by ensuring lockout, revoked/replayed refresh, and session-version mismatch tests assert structured unauthorized `ApiResponse<object>` outputs (non-enumerating) and pass.

## Observability / Diagnostics

- Runtime signals: customer `failed_login_count`, `lockout_end_utc`, `last_login_at_utc`, `token_version` and auth-session `revoked_at_utc` / `replaced_by_session_id` transitions.
- Inspection surfaces: `customers` and `auth_sessions` tables, controller test assertions, and structured unauthorized `ApiResponse<object>` payloads.
- Failure visibility: lockout branch, replay/revoked refresh branch, and session-version mismatch branch are all asserted by named tests.
- Redaction constraints: never log/return raw refresh tokens, token hashes, JWT secrets, or plaintext passwords.

## Integration Closure

- Upstream surfaces consumed: `IJwtTokenService`, `IRefreshTokenCookieService`, `IAccessTokenSessionValidator`, auth claim/role/policy constants, `Customer` normalized-email invariant.
- New wiring introduced in this slice: `CustomerAuthController` endpoints + contracts, customer credential schema/migration, refresh/logout session-state transitions.
- What remains before the milestone is truly usable end-to-end: AUTH-06 password-reset flow (later slice), S04 admin auth/RBAC completion, S05 frontend integration.

## Tasks

- [x] **T01: Make Customer credential-ready and harden normalized identity lookups** `est:1h30m`
  - Why: AUTH-01/AUTH-02 are blocked until customer rows can store credentials and reservation-created rows cannot fork accounts by email casing.
  - Files: `backend/src/RentACar.Core/Entities/Customer.cs`, `backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs`, `backend/src/RentACar.API/Services/ReservationService.cs`, `backend/src/RentACar.Infrastructure/Data/Migrations/*_AddCustomerAuthCredentials.cs`, `backend/src/RentACar.Infrastructure/Data/Migrations/RentACarDbContextModelSnapshot.cs`, `backend/tests/RentACar.Tests/Unit/Entities/PrincipalAuthStateTests.cs`, `backend/tests/RentACar.Tests/Unit/Services/ReservationServiceTests.cs`
  - Do: add customer credential persistence (`password_hash`) while keeping reservation-created customers valid, enforce normalized-email lookup in customer resolution paths, and update tests/migration artifacts to lock the invariant.
  - Verify: `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests|FullyQualifiedName~ReservationServiceTests"`
  - Done when: customer schema includes credential storage, reservation lookup is normalized-email based, and tests prove no case-drift duplicate-customer behavior.

- [x] **T02: Implement customer register/login/me endpoints with lockout policy** `est:2h`
  - Why: this delivers AUTH-01/AUTH-02 and AUTH-10 directly, while proving S02 token/session primitives work for customer principals.
  - Files: `backend/src/RentACar.API/Controllers/CustomerAuthController.cs`, `backend/src/RentACar.API/Contracts/Auth/CustomerRegisterRequest.cs`, `backend/src/RentACar.API/Contracts/Auth/CustomerLoginRequest.cs`, `backend/src/RentACar.API/Contracts/Auth/CustomerAuthResponse.cs`, `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs`, `backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs`
  - Do: add register/login/me endpoints under `api/customer/v1/auth`, use normalized-email principal lookup, hash passwords with `IPasswordHasher`, enforce 5-attempt lockout transitions, issue access+refresh through S02 services, persist session rows, and keep unauthorized responses non-enumerating.
  - Verify: `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~CustomerAuthControllerTests"`
  - Done when: register/login/me contracts pass with real assertions for success, invalid credentials, lockout, role claim/policy compatibility, and cookie/session persistence.

- [x] **T03: Add customer refresh/logout session rotation and lifecycle verification** `est:2h`
  - Why: AUTH-04/AUTH-05 require server-side refresh rotation/replay handling and revocation-aware logout, not cookie-only behavior.
  - Files: `backend/src/RentACar.API/Controllers/CustomerAuthController.cs`, `backend/src/RentACar.API/Contracts/Auth/CustomerRefreshResponse.cs`, `backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs`, `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs`, `backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs`
  - Do: implement refresh endpoint using refresh-cookie + hashed-session lookup with replay-safe rotation semantics, mark replaced/revoked sessions coherently, implement logout revocation using `sid`/principal context plus cookie clear, and expand tests for replay/revoked/expired failure branches.
  - Verify: `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~CustomerAuthControllerTests|FullyQualifiedName~AccessTokenSessionValidatorTests|FullyQualifiedName~JwtTokenServiceTests"`
  - Done when: refreshed tokens require active non-replayed session state, revoked sessions are rejected by validator, logout revokes session + clears cookie, and failure paths stay generic.

## Files Likely Touched

- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs`
- `backend/src/RentACar.API/Contracts/Auth/CustomerRegisterRequest.cs`
- `backend/src/RentACar.API/Contracts/Auth/CustomerLoginRequest.cs`
- `backend/src/RentACar.API/Contracts/Auth/CustomerAuthResponse.cs`
- `backend/src/RentACar.API/Contracts/Auth/CustomerRefreshResponse.cs`
- `backend/src/RentACar.Core/Entities/Customer.cs`
- `backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs`
- `backend/src/RentACar.API/Services/ReservationService.cs`
- `backend/src/RentACar.Infrastructure/Data/Migrations/*_AddCustomerAuthCredentials.cs`
- `backend/src/RentACar.Infrastructure/Data/Migrations/RentACarDbContextModelSnapshot.cs`
- `backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs`
- `backend/tests/RentACar.Tests/Unit/Entities/PrincipalAuthStateTests.cs`
- `backend/tests/RentACar.Tests/Unit/Services/ReservationServiceTests.cs`
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs`
- `backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs`
