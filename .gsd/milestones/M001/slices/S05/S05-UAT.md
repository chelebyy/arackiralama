# S05: Auth Frontend Integration — UAT

**Milestone:** M001
**Written:** 2026-03-15

## UAT Type

- UAT mode: mixed (live-runtime + artifact-driven)
- Why this mode is sufficient: S05 spans UI forms, proxy routing, and cookie-backed session behavior; both browser flow checks and command/test artifacts are needed.

## Preconditions

1. Backend API is running at `http://localhost:5135` with S04 contracts available.
2. Frontend is running at `http://localhost:3000`.
3. Seed users are available:
   - Active Admin/SuperAdmin user (known credentials)
   - Active Customer user (known credentials)
4. Browser starts with cleared auth cookies (`rac_access`, `rac_refresh`, `__Host-rac_refresh`).
5. If validating reset confirm, obtain a valid reset token from logs/test harness.

## Smoke Test

1. Open `http://localhost:3000/dashboard/default` as guest.
2. **Expected:** automatic redirect to `/dashboard/login/v2` and Admin Login form is visible.

## Test Cases

### 1. Admin login + protected dashboard access

1. Go to `/dashboard/login/v2`.
2. Enter valid admin credentials and submit.
3. **Expected:** user is redirected to `/dashboard/default` and sidebar/user menu renders.

### 2. Customer login + customer-only route behavior

1. Go to `/dashboard/login/v1`.
2. Enter valid customer credentials and submit.
3. **Expected:** user is redirected to `/dashboard/customer-portal`.
4. Navigate manually to `/dashboard/default`.
5. **Expected:** redirected to `/dashboard/forbidden` (customer blocked from admin dashboard).

### 3. Forgot password request (non-enumerating UX)

1. Open `/dashboard/forgot-password`.
2. Choose `Admin / SuperAdmin`, submit with unknown email.
3. Repeat with known admin email.
4. **Expected:** both submissions return same user-facing success behavior (no account existence leak).

### 4. Reset password confirm flow

1. Open `/dashboard/reset-password?scope=Admin&token=<valid_token>`.
2. Enter matching new password + confirmation and submit.
3. **Expected:** success toast/message and redirect to `/dashboard/login/v2`.
4. Attempt login with old password.
5. **Expected:** login fails.
6. Attempt login with new password.
7. **Expected:** login succeeds.

### 5. Logout cleanup

1. Login as admin and confirm dashboard is accessible.
2. Use user menu → `Log out`.
3. **Expected:** redirect to `/dashboard/login/v2`.
4. Open `/dashboard/default` directly.
5. **Expected:** redirected to login (session no longer valid in UI).

### 6. Guest route guard consistency

1. As guest, visit each route:
   - `/dashboard/default`
   - `/dashboard/pages/users`
   - `/dashboard/payment`
2. **Expected:** each route redirects to `/dashboard/login/v2` with `next=` query preserved.

## Edge Cases

### Expired access cookie with valid refresh cookie

1. Establish logged-in admin session.
2. Force access cookie expiry (or wait past TTL) while keeping refresh cookie.
3. Navigate to `/dashboard/default`.
4. **Expected:** request succeeds via refresh continuity (no forced re-login), and new access cookie is issued.

### Admin on superadmin-only route prefix

1. Login with role `Admin` (not `SuperAdmin`).
2. Navigate to `/dashboard/pages/users`.
3. **Expected:** redirected to `/dashboard/forbidden`.

## Failure Signals

- Protected routes render to guest users without redirect.
- Customer can access admin dashboards.
- Admin login/customer login do not call `/api/auth/login` or fail with schema mismatches.
- Forgot-password reveals account existence via different UI messages/status.
- Logout returns to login but protected routes remain accessible without re-authentication.
- Expired access cookie causes hard logout even when refresh cookie is valid.

## Requirements Proved By This UAT

- AUTH-06 — forgot/reset password frontend flow is integrated and executable.
- AUTH-07 — admin login flow works from UI to protected dashboard access.
- AUTH-09 — role-aware route protection works for guest/customer/admin/superadmin boundaries.
- AUTH-02/03/04/05/10 (frontend continuity) — login, expiry/refresh continuity, logout cleanup, and lockout-compatible generic UX.

## Not Proven By This UAT

- Real outbound email deliverability/inbox arrival for reset mail (dispatcher transport deferred beyond S05).
- Performance/load/security non-functional goals outside auth integration scope.

## Notes for Tester

- Current customer authenticated area is a functional proof route (`/dashboard/customer-portal`) to validate RBAC/session continuity.
- For deterministic reset validation, prefer test-harness-provided token capture over mailbox flow until notification slices ship.
