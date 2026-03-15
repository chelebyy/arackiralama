---
estimated_steps: 4
estimated_files: 4
---

# T01: Close admin auth lifecycle parity with lockout + refresh rotation

**Slice:** S04 — Admin Auth, RBAC & Password Reset Backend
**Milestone:** M001

## Description

Bring admin auth behavior to parity with the customer lifecycle already proven in S03: normalized identity lookup, deterministic lockout state mutation, refresh rotation lineage, and server-side logout revocation.

## Steps

1. Update `AdminAuthController` login flow to query by `NormalizedEmail`, enforce lockout threshold/duration mutations, and reset lockout counters on successful auth.
2. Add admin refresh endpoint + response contract using refresh-cookie lookup, hashed token verification, and session rotation (`revoked_at_utc` + `replaced_by_session_id`).
3. Update admin logout to revoke the active DB session using authenticated principal/session claims before clearing refresh cookie.
4. Extend controller/service tests to assert success + failure branches: lockout, normalized email acceptance, replay/revoked/expired refresh rejection, and session revocation on logout.

## Must-Haves

- [ ] Admin login is case-insensitive via normalized email and mutates `failed_login_count`/`lockout_end_utc` deterministically.
- [ ] Admin refresh only succeeds for active non-replaced sessions and rotates to a new session row.
- [ ] Admin logout revokes persisted session state (not cookie-only) and keeps unauthorized failures generic.

## Verification

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests|FullyQualifiedName~AccessTokenSessionValidatorTests"`
- Assertions in `AdminAuthControllerTests` prove lockout + refresh replay/revocation branches and DB session transition state.

## Observability Impact

- Signals added/changed: `admin_users.failed_login_count`, `admin_users.lockout_end_utc`, `admin_users.last_login_at_utc`, and `auth_sessions.revoked_at_utc/replaced_by_session_id` transitions for admin principal.
- How a future agent inspects this: run the targeted tests and inspect in-memory `AdminUsers` + `AuthSessions` entities asserted in test bodies.
- Failure state exposed: explicit unauthorized branches for lockout/replay/revoked/expired refresh and missing claim/session mismatch on logout.

## Inputs

- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — current login/me/logout baseline with missing refresh and lockout mutation.
- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs` — canonical session rotation + logout revocation pattern from S03.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs` — existing admin auth tests to extend.
- `.gsd/milestones/M001/slices/S03/S03-SUMMARY.md` — authoritative lineage/non-enumeration patterns to preserve.

## Expected Output

- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — normalized login, lockout state transitions, refresh rotation, and session-revoking logout.
- `backend/src/RentACar.API/Contracts/Auth/AdminRefreshResponse.cs` — refresh success contract for admin path.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs` — expanded lifecycle and failure-path coverage.
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs` — admin-oriented session-replacement/revocation guard coverage where needed.
