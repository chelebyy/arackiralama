# S04: Admin Auth, RBAC & Password Reset Backend — UAT

**Milestone:** M001
**Written:** 2026-03-15

## UAT Type

- UAT mode: mixed (live-runtime API checks + artifact-backed assertions)
- Why this mode is sufficient: S04 is security/state-transition heavy; confidence requires both endpoint behavior checks and persisted-state/test proof for lockout, session lineage, token lifecycle, and RBAC metadata.

## Preconditions

1. API is running locally (`dotnet run --project backend/src/RentACar.API`) and reachable.
2. Test database is available and migrations are applied.
3. Seed data includes:
   - one active **SuperAdmin** account
   - one active **Admin** account
   - one active **Customer** account
4. You can inspect DB tables (`admin_users`, `auth_sessions`, `password_reset_tokens`, `customers`) for state assertions.
5. Use a client that can store/send cookies (Postman/curl with cookie jar).

## Smoke Test

1. Login as SuperAdmin via `POST /api/admin/v1/auth/login` with valid credentials.
2. Call `GET /api/admin/v1/users` with returned bearer access token.
3. **Expected:** login returns access token + refresh cookie; users endpoint returns `200 OK` with admin user list.

## Test Cases

### 1. Admin login lockout lifecycle (AUTH-07, AUTH-10)

1. Send 5 consecutive invalid-password login attempts to `POST /api/admin/v1/auth/login` for the same active admin email.
2. After each failure, inspect `admin_users.failed_login_count` and `admin_users.lockout_end_utc`.
3. Attempt login with correct password before lockout expires.
4. **Expected:** first 4 attempts increment `failed_login_count`; 5th sets `lockout_end_utc` (~15 min). Correct credentials during active lockout are denied with generic unauthorized response.

### 2. Admin refresh rotation + replay rejection (AUTH-07)

1. Login as admin and capture refresh cookie value A.
2. Call `POST /api/admin/v1/auth/refresh` with cookie A.
3. Capture new refresh cookie value B and inspect old/new session rows in `auth_sessions`.
4. Retry refresh again using old cookie A.
5. **Expected:** first refresh succeeds and rotates session lineage (`old.revoked_at_utc` set, `old.replaced_by_session_id` points to new row). Reusing cookie A fails with generic unauthorized response.

### 3. Admin logout revokes server session (AUTH-07)

1. Login as admin and capture access token + `sid` claim + refresh cookie.
2. Call `POST /api/admin/v1/auth/logout` with bearer token and refresh cookie.
3. Inspect session row for the token `sid` in `auth_sessions`.
4. Attempt refresh using the old refresh cookie.
5. **Expected:** logout returns success and clears cookie; session row is revoked (`revoked_at_utc` set); subsequent refresh fails as unauthorized.

### 4. Password reset request is non-enumerating (AUTH-06)

1. Call `POST /api/v1/auth/password-reset/request` with `PrincipalScope=Admin` for:
   - unknown email
   - inactive admin email
   - active admin email
2. Compare HTTP status/body for all three calls.
3. For active admin only, inspect `password_reset_tokens` and dispatcher logs.
4. **Expected:** all branches return the same generic success payload/status. Only active principal path persists a new hashed token row and triggers dispatcher invocation.

### 5. Password reset confirm consumes token and invalidates auth state (AUTH-06)

1. Create/request a valid reset token for an active admin.
2. Confirm via `POST /api/v1/auth/password-reset/confirm` with valid token + strong new password + `PrincipalScope=Admin`.
3. Inspect `password_reset_tokens.consumed_at_utc`, `admin_users.token_version`, and active `auth_sessions` for that principal.
4. Attempt login with old password (should fail), then with new password (should succeed).
5. **Expected:** token is consumed once, `token_version` increments, active sessions are revoked, old password rejected, new password accepted.

### 6. SuperAdmin-only admin user management + canonical role enforcement (AUTH-08, AUTH-09)

1. As SuperAdmin, call:
   - `POST /api/admin/v1/users` (create with role `Admin`)
   - `PUT /api/admin/v1/users/{id}/role` (update to `SuperAdmin`)
   - `POST /api/admin/v1/users/{id}/deactivate`
   - `POST /api/admin/v1/users/{id}/activate`
   - `POST /api/admin/v1/users/{id}/reset-password`
2. Attempt same calls using an Admin token (non-superadmin).
3. Attempt create/update with non-canonical roles (`admin`, `SUPERADMIN`, `Manager`).
4. **Expected:** SuperAdmin calls succeed with persisted role/active-state mutations and reset-token creation+dispatch. Admin token is forbidden. Non-canonical roles are rejected with bad request.

## Edge Cases

### Password reset confirm with invalid/expired/consumed token

1. Call confirm with (a) random token, (b) expired token, (c) previously consumed token.
2. Inspect corresponding token row state after each call.
3. **Expected:** all fail with bad request; invalid token creates no state changes; expired token remains unconsumed; consumed token stays consumed with no second mutation.

### Admin refresh with revoked/replaced session

1. Reuse a refresh token after successful rotation or after logout revocation.
2. **Expected:** request is rejected as unauthorized; no new session row is created.

### Policy drift guard (artifact check)

1. Run:
   - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~RbacPolicyMatrixTests|FullyQualifiedName~AuthConventionsTests"`
2. **Expected:** all tests pass; any endpoint-policy mismatch fails immediately.

## Failure Signals

- Login succeeds during active lockout window.
- Replayed refresh token still issues new tokens.
- Logout clears cookie but leaves DB session active.
- Password reset request response differs between unknown vs known principals (enumeration leak).
- Confirm accepts expired/consumed token or does not bump `token_version`.
- Admin (non-superadmin) can access `api/admin/v1/users` management actions.
- Role inputs with case drift (`admin`, `SUPERADMIN`) are accepted.

## Requirements Proved By This UAT

- AUTH-06 — password reset lifecycle works with non-enumerating request semantics, secure token consumption rules, and post-reset invalidation.
- AUTH-07 — admin login/refresh/logout lifecycle is closed with lockout, rotation, replay protection, and revocation.
- AUTH-08 — SuperAdmin can manage admin users and trigger admin reset initiation.
- AUTH-09 — role boundaries (Guest/Customer/Admin/SuperAdmin) are enforced on protected endpoints.
- AUTH-10 — 5-attempt lockout behavior remains enforced for admin auth path.

## Not Proven By This UAT

- Email transport/provider deliverability, template rendering, inbox placement, and retry behavior (dispatcher is currently logging-backed stub).
- Frontend UX integration for admin login/reset/route guards (owned by S05).

## Notes for Tester

- Keep response checks strict for generic unauthorized/non-enumerating contracts: payloads should not reveal whether a principal exists.
- Capture DB state before and after each auth-stateful operation; S04’s value is in persisted transition correctness, not only HTTP status codes.
- If runtime setup is unavailable, use the slice verification test suite as authoritative artifact evidence for expected behavior.