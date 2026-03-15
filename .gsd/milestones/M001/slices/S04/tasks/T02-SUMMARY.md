---
id: T02
parent: S04
milestone: M001
provides:
  - Principal-aware password-reset request/confirm flow for Admin/Customer with hash-at-rest reset tokens, single-use confirm semantics, token-version/session invalidation, and executable email dispatch contract
key_files:
  - backend/src/RentACar.API/Controllers/PasswordResetController.cs
  - backend/src/RentACar.API/Contracts/Auth/PasswordResetRequest.cs
  - backend/src/RentACar.API/Contracts/Auth/PasswordResetConfirmRequest.cs
  - backend/src/RentACar.API/Services/IPasswordResetEmailDispatcher.cs
  - backend/src/RentACar.API/Services/PasswordResetEmailDispatcher.cs
  - backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/PasswordResetControllerTests.cs
key_decisions:
  - D019: Password-reset request persists hashed tokens and preserves non-enumerating success even when dispatch fails (warning-log only)
patterns_established:
  - Password reset request path resolves principal via normalized email + explicit `PrincipalScope` (`Admin`/`Customer`) and emits one generic success payload for unknown/inactive/valid branches
  - Confirm path validates token hash/expiry/consumed state, consumes once via `TryConsume`, then atomically updates password + principal `TokenVersion` + active-session revocation
  - Dispatch path is abstracted behind `IPasswordResetEmailDispatcher` and registered in DI for runtime execution and test mocking
observability_surfaces:
  - `password_reset_tokens.token_hash`, `password_reset_tokens.expires_at_utc`, `password_reset_tokens.consumed_at_utc`
  - `admin_users.token_version` / `customers.token_version` increments after successful confirm
  - `auth_sessions.revoked_at_utc` updates for active sessions of the reset principal
  - `PasswordResetControllerTests` branch assertions for unknown/inactive/valid request flows, dispatch invocation, and invalid/expired/consumed/valid confirm paths
duration: 58m
verification_result: passed
completed_at: 2026-03-15
blocker_discovered: false
---

# T02: Implement principal-aware password reset request/confirm with email dispatch contract

**Implemented end-to-end principal-aware password reset backend flow with secure token lifecycle (hash-at-rest, expiry, single-use consume), non-enumerating request semantics, dispatch abstraction, and post-confirm principal/session invalidation.**

## What Happened

- Added auth contracts:
  - `PasswordResetRequest` (`Email`, `PrincipalScope`)
  - `PasswordResetConfirmRequest` (`Token`, `NewPassword`, `PrincipalScope`)
- Implemented `PasswordResetController` with:
  - `POST /api/v1/auth/password-reset/request`:
    - validates input + explicit scope
    - resolves principal by normalized email and scope
    - creates/persists only hashed token (`token_hash`) with expiry (`expires_at_utc`)
    - invokes `IPasswordResetEmailDispatcher`
    - returns same success payload for unknown/inactive/valid principal branches
  - `POST /api/v1/auth/password-reset/confirm`:
    - validates scope + token/new password
    - matches stored token by hash/scope
    - rejects invalid/expired/consumed tokens
    - consumes token once (`ConsumedAtUtc` set)
    - updates principal password hash
    - increments principal `TokenVersion`
    - revokes active `AuthSessions` for same principal
- Added dispatch abstraction and executable default implementation:
  - `IPasswordResetEmailDispatcher`
  - `PasswordResetEmailDispatcher` (logging-backed stub)
  - DI registration in `ServiceCollectionExtensions`
- Added comprehensive controller tests covering:
  - request non-enumeration (unknown/inactive/valid)
  - dispatch invocation on valid branch
  - valid confirm state transitions
  - invalid/expired/consumed confirm rejections

## Verification

Executed required task + slice verification commands:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PasswordResetControllerTests"` ✅ (7 passed, 0 failed)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests|FullyQualifiedName~AccessTokenSessionValidatorTests"` ✅ (19 passed, 0 failed)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminUsersControllerTests|FullyQualifiedName~RbacPolicyMatrixTests|FullyQualifiedName~AuthConventionsTests"` ✅ (6 passed, 0 failed)

Observability-impact assertions are explicitly covered in tests:
- `password_reset_tokens` row checks (`token_hash`, `expires_at_utc`, `consumed_at_utc`)
- post-confirm `admin_users.token_version` bump
- post-confirm active `auth_sessions.revoked_at_utc` mutation for reset principal

## Diagnostics

Fastest inspection surfaces:
- `backend/tests/RentACar.Tests/Unit/Controllers/PasswordResetControllerTests.cs`
  - `Request_WithUnknownEmail_ReturnsNonEnumeratingSuccessAndSkipsDispatch`
  - `Request_WithInactiveAdmin_ReturnsNonEnumeratingSuccessAndSkipsDispatch`
  - `Request_WithActiveAdmin_PersistsHashedTokenAndInvokesDispatcher`
  - `Confirm_WithValidAdminToken_ConsumesTokenBumpsTokenVersionAndRevokesActiveSessions`
  - `Confirm_WithInvalidToken_ReturnsBadRequestAndDoesNotMutateState`
  - `Confirm_WithExpiredToken_ReturnsBadRequestAndKeepsTokenUnconsumed`
  - `Confirm_WithConsumedToken_ReturnsBadRequestAndKeepsConsumedState`

## Deviations

- Scope was implemented as explicit `PrincipalScope` string values (`"Admin"`/`"Customer"`) in request contracts rather than a JSON enum type to keep API contracts explicit and avoid serializer enum-format ambiguity.

## Known Issues

- `PasswordResetEmailDispatcher` is a minimal logging-backed implementation; real outbound email transport/template wiring remains future work.

## Files Created/Modified

- `backend/src/RentACar.API/Controllers/PasswordResetController.cs` — added request/confirm endpoints with secure token lifecycle and principal/session invalidation.
- `backend/src/RentACar.API/Contracts/Auth/PasswordResetRequest.cs` — added reset request contract.
- `backend/src/RentACar.API/Contracts/Auth/PasswordResetConfirmRequest.cs` — added reset confirm contract.
- `backend/src/RentACar.API/Services/IPasswordResetEmailDispatcher.cs` — added dispatch abstraction.
- `backend/src/RentACar.API/Services/PasswordResetEmailDispatcher.cs` — added concrete executable dispatcher stub.
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — registered password reset dispatcher service.
- `backend/tests/RentACar.Tests/Unit/Controllers/PasswordResetControllerTests.cs` — added full controller lifecycle/failure-path coverage.
- `.gsd/DECISIONS.md` — appended D019.
- `.gsd/milestones/M001/slices/S04/S04-PLAN.md` — marked T02 complete.
- `.gsd/STATE.md` — advanced next action to T03 and recorded recent decision.
