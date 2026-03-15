# S03: Customer Auth API — UAT

**Milestone:** M001
**Written:** 2026-03-15

## UAT Type

- UAT mode: mixed (live-runtime + artifact-driven)
- Why this mode is sufficient: customer-auth behavior is API-first; runtime API calls prove endpoint contracts while DB/session-state assertions and automated tests prove lockout/rotation/revocation invariants.

## Preconditions

- Backend API is running (`dotnet run --project backend/src/RentACar.API`) and reachable.
- Database schema includes `customers.password_hash` migration from S03.
- JWT + refresh cookie settings are configured from S02.
- Test client can store/send cookies (Postman, curl with `-c/-b`, or similar).
- Start with a clean test identity (e.g., `s03.customer+001@example.com`).

## Smoke Test

1. `POST /api/customer/v1/auth/register` with new email/password.
2. `POST /api/customer/v1/auth/login` with same credentials and capture access token + refresh cookie.
3. `GET /api/customer/v1/auth/me` with bearer token.
4. **Expected:** all three calls succeed (`200`/`201` depending implementation), and `me` returns the authenticated customer principal.

## Test Cases

### 1. Register new customer (AUTH-01)

1. Call `POST /api/customer/v1/auth/register` with:
   - `email`: new unique email
   - `password`: valid password
   - optional profile fields (name/phone)
2. Query `customers` table for normalized-email match.
3. **Expected:**
   - API success response with customer identity payload.
   - `customers.password_hash` is populated (never plaintext).
   - `normalized_email` is uppercase normalized form.

### 2. Register upgrades reservation-created guest record

1. Seed/create a customer row with same normalized email but `password_hash = null` (reservation-created style).
2. Call `POST /api/customer/v1/auth/register` with matching email (any casing).
3. Re-query the same `customers.id` row.
4. **Expected:**
   - Existing row is upgraded in place (no duplicate row created).
   - `password_hash` becomes non-null.
   - Optional profile fields are updated per controller rules.

### 3. Login success issues session-backed credentials (AUTH-02/AUTH-03)

1. Call `POST /api/customer/v1/auth/login` with valid credentials.
2. Capture access token and refresh cookie.
3. Query `auth_sessions` for `principal_type = Customer` and returned `sid`.
4. **Expected:**
   - API success response contains access token and expiry metadata.
   - Refresh cookie is appended (`HttpOnly` policy from S02 service).
   - Session row exists and is active (not revoked, not replaced).
   - Customer lockout counters reset (`failed_login_count = 0`, `lockout_end_utc = null`), `last_login_at_utc` updated.

### 4. Failed login lockout after 5 attempts (AUTH-10)

1. Use an existing credentialed customer.
2. Call `POST /api/customer/v1/auth/login` with wrong password five times.
3. Query customer row after each attempt.
4. Attempt login with the correct password while lockout window is active.
5. **Expected:**
   - Each failure returns generic unauthorized `ApiResponse<object>` (non-enumerating).
   - `failed_login_count` increments on each failed attempt.
   - On attempt 5, `lockout_end_utc` is set to `now + ~15 minutes`.
   - Correct-password login during lockout still returns generic unauthorized.

### 5. Refresh rotates session lineage and token material (AUTH-04)

1. Login and capture initial refresh cookie/token and current session id (`sid`).
2. Call `POST /api/customer/v1/auth/refresh` using the refresh cookie.
3. Query prior and new session rows.
4. **Expected:**
   - API success response returns new access token contract (`CustomerRefreshResponse`).
   - Prior session has `revoked_at_utc` set and `replaced_by_session_id` populated.
   - Replacement session is active with new refresh token hash.
   - Response sets a rotated refresh cookie.

### 6. Refresh replay rejection

1. Perform successful refresh once (test case 5).
2. Reuse the old refresh token/cookie value from before rotation.
3. **Expected:**
   - Generic unauthorized `ApiResponse<object>` (no replay details leaked).
   - No new active session is created from replayed token.

### 7. Logout revokes current session and clears refresh cookie (AUTH-05)

1. Login and call `POST /api/customer/v1/auth/logout` with valid bearer token.
2. Query session row by `sid`.
3. Attempt refresh with the previously issued refresh cookie.
4. **Expected:**
   - Logout call succeeds.
   - Session is revoked (`revoked_at_utc` set).
   - Refresh cookie is cleared in response.
   - Subsequent refresh attempt fails with generic unauthorized.

### 8. Me endpoint policy enforcement (AUTH-09 partial)

1. Call `GET /api/customer/v1/auth/me` with:
   - no token
   - a valid customer token
2. **Expected:**
   - No token => unauthorized envelope.
   - Valid customer token => success with current customer principal identity.

## Edge Cases

### Already-registered email should not enumerate account existence

1. Register a credentialed customer.
2. Call register again with same normalized email.
3. **Expected:** generic unauthorized response; no explicit “email exists” leak; no credential/session mutation.

### Guest row without password cannot authenticate

1. Ensure customer exists with `password_hash = null`.
2. Attempt login with any password.
3. **Expected:** generic unauthorized response and no lockout counter mutation.

### Expired refresh session rejected

1. Create/login a session, then force `expires_at_utc` into the past.
2. Call refresh.
3. **Expected:** generic unauthorized response; no rotated session created.

## Failure Signals

- Non-generic auth failures exposing account existence details.
- `failed_login_count` or `lockout_end_utc` not mutating on repeated failed logins.
- Refresh succeeds without revoking/replacing old session.
- Replayed refresh token can mint new credentials.
- Logout only clears cookie but leaves current DB session active.

## Requirements Proved By This UAT

- AUTH-01 — customer registration works with credential persistence.
- AUTH-02 — customer login works with email/password.
- AUTH-03 — customer receives valid short-lived access JWT.
- AUTH-04 — refresh works within active session lifetime and rotates safely.
- AUTH-05 — logout revokes server session and invalidates refresh path.
- AUTH-10 — 5-attempt lockout enforced.
- AUTH-09 (partial) — customer-protected endpoint access is enforced.

## Not Proven By This UAT

- AUTH-06 — password reset via email link.
- AUTH-07 / AUTH-08 — admin and superadmin auth flows.
- Full AUTH-09 across Admin/SuperAdmin policy matrix.

## Notes for Tester

- Use dedicated test identities per case to avoid lockout/session-state cross contamination.
- Never log raw refresh tokens or password values in shared logs.
- For deterministic replay testing, store both “before refresh” and “after refresh” cookie values explicitly.
