---
estimated_steps: 5
estimated_files: 6
---

# T02: Implement customer register/login/me endpoints with lockout policy

**Slice:** S03 — Customer Auth API
**Milestone:** M001

## Description

Implement customer-facing auth entrypoints for account creation and authentication using S02 primitives. This task directly delivers register/login behavior and enforces lockout after repeated failed logins.

## Steps

1. Add customer auth request/response contracts and scaffold `CustomerAuthController` under `api/customer/v1/auth`.
2. Implement **register** flow with normalized-email lookup, guest-record upgrade semantics, password hashing, and deterministic success/failure responses.
3. Implement **login** flow with normalized lookup, password verification, failed-attempt increment, lockout-at-5 policy, lockout reset on success, and session/token/cookie issuance.
4. Implement **me** endpoint secured by `CustomerOnly` policy to validate customer principal issuance/authorization compatibility.
5. Add controller tests covering register/login/me success and failure branches (including lockout and non-enumerating unauthorized behavior).

## Must-Haves

- [ ] Register endpoint creates or upgrades a customer account using normalized-email identity rules and hashed passwords.
- [ ] Login endpoint enforces 5-attempt lockout and resets lockout counters on successful authentication.
- [ ] Successful login persists `AuthSession`, emits customer access token, and sets refresh cookie via `IRefreshTokenCookieService`.
- [ ] Unauthorized/failure paths keep generic response shape and do not leak account existence.

## Verification

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~CustomerAuthControllerTests"`
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests"`

## Observability Impact

- Signals added/changed: `failed_login_count`, `lockout_end_utc`, `last_login_at_utc`, and new customer session creation events in `auth_sessions`.
- How a future agent inspects this: controller tests + DB assertions against `customers` and `auth_sessions` state after login attempts.
- Failure state exposed: locked account path, invalid-password path, and missing-credential path are distinguishable in tests while returning generic unauthorized payloads externally.

## Inputs

- `.gsd/milestones/M001/slices/S03/tasks/T01-PLAN.md` — credential persistence and normalized-lookup preconditions.
- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — current auth-controller behavior patterns.
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — policy/rate-limit/auth pipeline conventions.
- `backend/src/RentACar.API/Services/IJwtTokenService.cs` — customer access/refresh token contract.

## Expected Output

- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs` — register/login/me endpoints.
- `backend/src/RentACar.API/Contracts/Auth/CustomerRegisterRequest.cs` — register request contract.
- `backend/src/RentACar.API/Contracts/Auth/CustomerLoginRequest.cs` — login request contract.
- `backend/src/RentACar.API/Contracts/Auth/CustomerAuthResponse.cs` — customer auth success payload.
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — any required auth wiring updates for customer endpoint policy usage.
- `backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs` — register/login/me contract and lockout tests.
