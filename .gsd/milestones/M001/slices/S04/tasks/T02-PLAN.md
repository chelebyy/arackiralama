---
estimated_steps: 5
estimated_files: 7
---

# T02: Implement principal-aware password reset request/confirm with email dispatch contract

**Slice:** S04 — Admin Auth, RBAC & Password Reset Backend
**Milestone:** M001

## Description

Implement AUTH-06 end-to-end with a secure token lifecycle and principal-aware scope. The reset flow must avoid account enumeration, persist only hashed tokens, support single-use confirm semantics, and invalidate all prior sessions after password change.

## Steps

1. Add password-reset request/confirm API contracts with explicit principal scope (`Customer` or `Admin`) and token/new-password inputs.
2. Implement `PasswordResetController` request endpoint that finds principal by normalized email + scope, stores hashed reset token + expiry, invokes email dispatch abstraction, and returns non-enumerating success response regardless of account existence.
3. Implement confirm endpoint that validates hash/expiry/consumed state, updates password hash, consumes token atomically, bumps principal `TokenVersion`, and revokes active sessions for the same principal.
4. Introduce/register `IPasswordResetEmailDispatcher` (minimal implementation acceptable) so dispatch path is executable and mockable in tests.
5. Add controller tests covering request non-enumeration, dispatch invocation, valid confirm, and invalid/expired/consumed token branches plus session/token-version invalidation.

## Must-Haves

- [ ] Reset request response is non-enumerating for unknown email, inactive principal, and valid principal branches.
- [ ] Reset tokens are hash-at-rest, expiring, and single-use (`ConsumedAtUtc` set once only).
- [ ] Successful confirm invalidates prior tokens/sessions by bumping principal `TokenVersion` and revoking active `AuthSessions`.

## Verification

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PasswordResetControllerTests"`
- Test assertions verify `password_reset_tokens` row state (`token_hash`, `expires_at_utc`, `consumed_at_utc`) and post-confirm `auth_sessions` revocation/token-version bump.

## Observability Impact

- Signals added/changed: password reset token lifecycle rows, principal token-version increments, and bulk session revocation after password reset.
- How a future agent inspects this: run `PasswordResetControllerTests` and inspect asserted in-memory table state for `PasswordResetTokens`, `AdminUsers`/`Customers`, and `AuthSessions`.
- Failure state exposed: explicit invalid/expired/consumed token rejection and dispatch-path invocation failures surfaced in test assertions.

## Inputs

- `backend/src/RentACar.Core/Entities/PasswordResetToken.cs` — domain semantics for active/consumed tokens.
- `backend/src/RentACar.Infrastructure/Data/Configurations/PasswordResetTokenConfiguration.cs` — persistence/index constraints for reset tokens.
- `backend/src/RentACar.API/Controllers/BaseApiController.cs` — generic unauthorized/response contract.
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — DI registration surface for new dispatcher service.
- `backend/tests/RentACar.Tests/TestFixtures/TestDbContextFactory.cs` — unit test persistence harness.

## Expected Output

- `backend/src/RentACar.API/Controllers/PasswordResetController.cs` — request/confirm endpoints with secure lifecycle behavior.
- `backend/src/RentACar.API/Contracts/Auth/PasswordResetRequest.cs` — request contract.
- `backend/src/RentACar.API/Contracts/Auth/PasswordResetConfirmRequest.cs` — confirm contract.
- `backend/src/RentACar.API/Services/IPasswordResetEmailDispatcher.cs` — dispatch abstraction.
- `backend/src/RentACar.API/Services/PasswordResetEmailDispatcher.cs` — concrete dispatch implementation/stub.
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — dispatcher registration.
- `backend/tests/RentACar.Tests/Unit/Controllers/PasswordResetControllerTests.cs` — full reset lifecycle test coverage.
