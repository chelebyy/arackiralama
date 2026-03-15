# Phase 6 Research: User Management & Auth

**Phase:** 6  
**Date:** 2026-03-14  
**Scope:** Customer + admin auth with JWT, RBAC, refresh tokens, logout/revocation, password reset, account lockout, and admin user management.

## Repo Baseline

### Existing reusable assets
- `JwtTokenService` already issues signed admin JWT access tokens.
- `ServiceCollectionExtensions` already wires `JwtBearer` auth and policy-based admin authorization.
- `BcryptPasswordHasher` is already the password hashing abstraction in the repo.
- `AdminAuthController` already defines the controller/contract pattern for auth endpoints.
- `AuditLog` infrastructure already exists and is used by services such as `FleetService`.
- `Customer` already exists and is the canonical reservation/customer record.
- Frontend already has reusable auth page templates under `frontend/app/(admin)/dashboard/(guest)/*`.

### Current gaps
- Admin auth is access-token only. No refresh token, no revocation, and logout is currently a no-op.
- Current admin login uses exact email match and does not track failed attempts or lockout.
- `JwtTokenService` only supports admin tokens and uses `Jwt:AccessTokenHours`; Phase 6 requires 15-minute access tokens.
- `Customer` has no auth fields yet.
- `Customer.Email` is indexed but not unique, which conflicts with login/register requirements.
- Reservation flow auto-creates customers by email, so auth must work with guest-created customer records.

## Standard Stack

- Backend auth stack: keep custom ASP.NET Core auth with `Microsoft.AspNetCore.Authentication.JwtBearer`, policy authorization, EF Core, PostgreSQL, and `BCrypt.Net-Next`.
- Token format: JWT access tokens for API authorization, opaque refresh tokens for session renewal.
- Refresh token persistence: database-backed session table with hashed refresh tokens.
- Password reset: database-backed single-use reset tokens, hashed at rest, 30-minute expiry.
- RBAC: keep ASP.NET Core `RequireRole` policies for `AdminOnly` and `SuperAdminOnly`; add customer-auth policy if needed.
- Frontend token handling: refresh token in `HttpOnly`, `Secure`, `SameSite=Lax/Strict` cookie; access token kept short-lived and out of `localStorage`.

### Official-framework guidance used
- ASP.NET Core docs support the current `JwtBearer` + `TokenValidationParameters` + `RequireRole` direction.
- ASP.NET Core docs support time-limited token patterns through Data Protection, but this repo should prefer DB-backed reset tokens over introducing Identity token providers mid-project.

## Architecture Patterns

### 1. Extend the existing custom auth path, do not adopt ASP.NET Identity

Do not introduce ASP.NET Identity in Phase 6.

Reasons:
- The repo already has custom entities, EF configuration, JWT issuance, and policy auth.
- Identity would force a parallel user model, larger migrations, and more surface area than this phase needs.
- The existing codebase favors explicit service/controller logic over framework-heavy identity abstractions.

### 2. Treat `Customer` as the customer principal

Do not create a separate customer-account root entity unless a later phase proves it necessary.

Recommended change:
- Extend `Customer` with nullable auth/account fields so guest-created customers can later become registered customers.

Recommended added customer fields:
- `PasswordHash`
- `IsEmailConfirmed` or `EmailConfirmedAtUtc`
- `FailedLoginCount`
- `LockoutEndUtc`
- `LastLoginAtUtc`
- `TokenVersion`

Rationale:
- Reservation flow already depends on `Customer`.
- Registration can claim an existing guest-created customer record by normalized email.
- This keeps customer history, reservations, and auth on the same principal.

### 3. Keep `AdminUser` as the admin principal

Extend `AdminUser` with the same auth-management fields needed for security and revocation:
- `FailedLoginCount`
- `LockoutEndUtc`
- `LastLoginAtUtc`
- `TokenVersion`

Keep admin and customer principals separate. Do not merge them into one polymorphic user table in this phase.

### 4. Add a shared auth-session table for refresh tokens and per-device logout

Add a new persistence model such as `AuthSession`:
- `Id`
- `PrincipalType` (`Customer` or `Admin`)
- `PrincipalId`
- `RefreshTokenHash`
- `RefreshTokenExpiresAtUtc`
- `CreatedAtUtc`
- `LastSeenAtUtc`
- `RevokedAtUtc`
- `ReplacedBySessionId`
- `CreatedByIp`
- `UserAgent`
- `SessionId` or `Sid`

Use one row per logged-in device/session.

This is required for:
- multi-device login
- current-device logout
- refresh token rotation
- revocation tracking
- admin deactivation session kill

### 5. Add a shared password-reset token table

Add a table such as `PasswordResetToken`:
- `Id`
- `PrincipalType`
- `PrincipalId`
- `TokenHash`
- `ExpiresAtUtc`
- `ConsumedAtUtc`
- `CreatedAtUtc`

Rules:
- generate a cryptographically random opaque token
- store only the hash
- single use
- 30-minute expiry

### 6. Make access-token revocation real

Short JWT expiry alone is not enough because this phase requires:
- logout
- password-reset logout-all-devices
- admin deactivation immediate cutoff

Recommended pattern:
- include `sid` and `ver` claims in access tokens
- `sid` maps to `AuthSession`
- `ver` maps to `TokenVersion` on the principal
- after JWT signature validation, perform app-level session validation against DB

This lets the system reject:
- revoked sessions
- rotated-out sessions
- all tokens issued before password reset or admin deactivation

## Recommended Domain Changes

### Customer
- Normalize email on write and lookup.
- Add a unique index on normalized email.
- Keep existing reservation fields unchanged.
- Support guest customers with `PasswordHash = null`.

### AdminUser
- Normalize email on write and lookup.
- Keep role as explicit string for now (`Admin`, `SuperAdmin`), since current policies already depend on it.
- Add lockout and token-version fields.

### Supporting tables
- `AuthSession`
- `PasswordResetToken`

No other new auth tables are necessary for Phase 6.

## Recommended Endpoint Shape

### Customer auth
- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`
- `POST /api/v1/auth/logout-all`
- `GET /api/v1/auth/me`
- `POST /api/v1/auth/forgot-password`
- `POST /api/v1/auth/reset-password`

### Admin auth
- Keep admin routes under `/api/admin/v1/auth/*`
- Add:
  - `POST /api/admin/v1/auth/refresh`
  - `POST /api/admin/v1/auth/logout-all` if needed
  - real revocation behavior for `logout`

### Admin user management
- `GET /api/admin/v1/users`
- `POST /api/admin/v1/users`
- `PATCH /api/admin/v1/users/{id}`
- `PATCH /api/admin/v1/users/{id}/deactivate`
- `PATCH /api/admin/v1/users/{id}/activate`
- `PATCH /api/admin/v1/users/{id}/role`

All admin-user-management endpoints should require `SuperAdminOnly`.

## Token and Session Flow

### Access token
- Lifetime: 15 minutes
- Claims:
  - `sub`
  - `email`
  - `role`
  - `sid`
  - `ver`
  - optional permission claims for admin APIs

### Refresh token
- Lifetime: 7 days
- Opaque random secret
- Stored hashed in `AuthSession`
- Rotated on every refresh
- Old refresh token revoked immediately on rotation

### Logout behavior
- Standard logout revokes only the current `AuthSession`
- Password reset increments principal `TokenVersion` and revokes all sessions
- Admin deactivation increments `TokenVersion` and revokes all sessions immediately

## Password Reset Design

Recommended flow:
1. User submits email.
2. API always returns generic success response.
3. If account exists, create single-use reset token row with 30-minute expiry.
4. Email contains opaque token and principal identifier payload.
5. Reset endpoint validates hash, expiry, and unused state.
6. Password change succeeds.
7. Increment `TokenVersion`.
8. Revoke all sessions.
9. Mark reset token consumed.

Use concise, security-first email copy. Do not leak whether the email exists.

## Lockout Design

Recommended default:
- lock after 5 failed attempts
- cooldown: 15 minutes

Behavior:
- increment failed count on invalid login
- set `LockoutEndUtc` when threshold reached
- reset count on successful login
- apply to both customer and admin login flows

Admin lockout must still respect `IsActive = false` as a harder denial.

## RBAC Model

Roles required by requirements:
- `Guest`
- `Customer`
- `Admin`
- `SuperAdmin`

Implementation direction:
- Guests are unauthenticated users, not DB-stored role rows.
- Customer role comes from successful customer auth.
- Admin and SuperAdmin remain explicit `AdminUser.Role` values.

Policy direction:
- keep `AdminOnly`
- keep `SuperAdminOnly`
- optionally add `CustomerOnly`

## Frontend Integration Pattern

### Admin
- Reuse existing guest admin pages for login/forgot-password.
- Replace static submit behavior with API-backed forms.
- Add admin session bootstrap on dashboard entry.
- Redirect unauthorized admin users to `/dashboard/login/*`.

### Customer/public
- Do not invent a second auth UI system.
- Reuse the same form primitives and validation approach already used on admin guest pages.
- Phase 6 should at least define the auth client and route contract, even if most public UX polish lands in Phase 8.

### Token handling
- Prefer cookie-backed refresh with silent refresh while active.
- Do not store refresh tokens in `localStorage`.
- Keep access tokens ephemeral and refresh-driven.

## Repo-Specific Planning Implications

### Service layout
- Expand `IJwtTokenService` and `JwtTokenService` for shared customer/admin token issuance.
- Move login/register/refresh/reset logic into dedicated auth services instead of growing controllers.
- Extract reusable audit logging helper if both auth and existing services need the same pattern.

### Data/migrations
- Customer email uniqueness is a migration risk and must be planned explicitly.
- Existing guest-created customers may need deduplication or a pre-migration data check before adding a unique index.
- New auth tables should use the same lowercase snake_case naming style as existing EF configs.

### Testing
- Add unit tests for token claims, refresh rotation, lockout, reset token validation, and logout revocation.
- Add controller tests for both admin and customer auth flows.
- Add integration tests for session validation on protected endpoints.

## Validation Architecture

Phase 6 should be validated as layered auth behavior, not just endpoint happy paths. Nyquist validation should treat each dimension below as a separate acceptance surface with explicit evidence.

### Dimension 1. Contract and requirement coverage
- Check every Phase 6 requirement (`AUTH-01` through `AUTH-10`) maps to at least one backend/API validation and one user-flow validation where applicable.
- Check deferred items (`AUTH-11` to `AUTH-14`) are still absent from scope and no partial social-login/2FA artifacts leaked in.
- Check route inventory matches the planned customer/admin auth surface and no required endpoint is missing.

### Dimension 2. Principal and data-model integrity
- Check `Customer` can support guest-to-registered evolution without breaking existing reservation ownership.
- Check customer email uniqueness is enforced with a migration-safe normalization strategy.
- Check `AdminUser` and `Customer` remain separate principals and role boundaries are not blurred.
- Check new session/reset-token tables use repo naming conventions and can represent revocation, rotation, expiry, and consumption cleanly.
- Check migration validation includes duplicate-email detection before unique index enforcement.

### Dimension 3. Authentication correctness
- Check customer registration creates or claims the correct principal and never creates duplicate accounts for the same normalized email.
- Check login accepts valid credentials, rejects invalid credentials, and treats email matching case-insensitively.
- Check access tokens contain the required claims (`sub`, `email`, `role`, `sid`, `ver`) and use the required short lifetime.
- Check refresh flow rotates refresh tokens on every use and rejects replay of the old token.
- Check logout revokes only the active session by default.

### Dimension 4. Authorization and RBAC enforcement
- Check unauthenticated users cannot access protected customer/admin routes.
- Check `Customer` tokens cannot access admin routes.
- Check `Admin` cannot perform `SuperAdmin`-only actions such as admin creation or role change.
- Check `SuperAdmin` can perform admin-user management actions end-to-end.
- Check deactivated admin accounts lose access immediately, including already-issued sessions.

### Dimension 5. Session lifecycle and revocation
- Check multiple concurrent customer sessions work when allowed by the phase decisions.
- Check session validation happens after JWT validation and rejects revoked or version-mismatched sessions.
- Check password reset invalidates all active sessions for the affected principal.
- Check admin deactivation invalidates all active sessions immediately.
- Check expired refresh tokens and revoked sessions fail closed with stable error responses.

### Dimension 6. Recovery and lockout behavior
- Check forgot-password always returns a generic response regardless of account existence.
- Check reset tokens are single-use, expire at 30 minutes, and are stored hashed at rest.
- Check successful password reset updates credentials and revokes prior sessions.
- Check failed login attempts increment correctly and lock the account after the configured threshold.
- Check lockout cooldown expires automatically and successful login resets the failure counter.

### Dimension 7. Security controls
- Check refresh and reset tokens are never stored raw in the database.
- Check refresh tokens are not exposed to frontend JavaScript if cookie-based storage is chosen.
- Check JWT secret/validation config still fails startup when secret quality is insufficient.
- Check auth endpoints remain under strict rate limiting and lockout cannot be bypassed via endpoint inconsistencies.
- Check auth responses do not leak whether an email exists, whether an admin is inactive, or whether a reset token was once valid.

### Dimension 8. Auditability and operational traceability
- Check critical auth events write `AuditLog` records: login success/failure where intended, logout, refresh, password reset request/complete, admin create/update/deactivate, role change.
- Check audit payloads identify actor, target, action, and timestamp with enough detail for Phase 8 audit views.
- Check security-significant actions are distinguishable from normal CRUD noise.

### Dimension 9. Frontend integration and user-flow validation
- Check admin login page, forgot-password page, and session bootstrap use the final backend contract rather than placeholder behavior.
- Check customer auth flow can coexist with current reservation flow without forcing login for guest bookings.
- Check expired access tokens recover through silent refresh while session is still valid.
- Check invalid/expired sessions redirect users cleanly instead of leaving stale authenticated UI state.

### Dimension 10. Regression and compatibility validation
- Check existing admin login still works after refresh/revocation additions.
- Check reservation flow that auto-creates `Customer` records still works for unauthenticated users.
- Check Phase 5 payment/reservation paths do not regress because of customer-model or auth middleware changes.
- Check existing tests for JWT issuance, password hashing, and admin auth are updated rather than silently bypassed.

### Suggested Nyquist evidence set
- Unit tests for token creation, password hashing integration, lockout counters, reset token lifecycle, and token-version/session checks.
- Integration tests for register/login/refresh/logout/reset flows across both customer and admin principals.
- Authorization tests proving `Guest`, `Customer`, `Admin`, and `SuperAdmin` boundaries.
- Migration verification script or query for duplicate customer emails before unique-index rollout.
- Manual or Playwright-backed UI checks for admin login/logout/reset and stale-session recovery.

### Suggested validation checkpoints for the later Phase 6 verifier
- `Checkpoint A`: schema + migration safety validated before implementation is considered complete.
- `Checkpoint B`: customer/admin auth happy paths validated with integration tests.
- `Checkpoint C`: revocation, reset, lockout, and deactivation security paths validated with negative tests.
- `Checkpoint D`: RBAC and audit-log coverage validated against requirements and Phase 8 dependency needs.

## Don't Hand-Roll

- Do not hand-roll password hashing. Keep `BCrypt.Net-Next`.
- Do not hand-roll JWT signature validation. Keep ASP.NET Core `JwtBearer`.
- Do not store raw refresh tokens or reset tokens in the database.
- Do not build custom role engines beyond existing ASP.NET Core policy/role checks.
- Do not introduce ASP.NET Identity just for reset/lockout features in this phase.

## Common Pitfalls

- Current admin email lookup is case-sensitive. That will cause false login failures.
- Current customer model allows non-unique emails. Auth cannot rely on that.
- Current logout endpoint does not revoke anything.
- JWTs are currently valid until expiry even after deactivation; Phase 6 must close that gap.
- Reservation flow auto-creates customers. Registration must handle existing customer rows cleanly.
- If refresh tokens are rotated without storing replacement/revocation state, replay attacks remain possible.
- If access tokens are stored in browser storage long-term, XSS impact becomes much worse.
- If password reset responses reveal account existence, email enumeration becomes trivial.
- If auth events are not written to `AuditLog`, Phase 8 admin audit views will be incomplete.

## Code Examples

### Access-token claims shape

```csharp
var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.Sub, principalId.ToString()),
    new(JwtRegisteredClaimNames.Email, email),
    new(ClaimTypes.NameIdentifier, principalId.ToString()),
    new(ClaimTypes.Name, email),
    new(ClaimTypes.Role, role),
    new("sid", sessionId.ToString()),
    new("ver", tokenVersion.ToString())
};
```

### Session validation after JWT validation

```csharp
// After JwtBearer validates signature/lifetime:
// 1. read sid + ver claims
// 2. load active session
// 3. load principal token version
// 4. reject request if session revoked or version mismatched
```

### Audit pattern

```csharp
dbContext.AuditLogs.Add(new AuditLog
{
    Action = "CustomerPasswordResetCompleted",
    EntityType = "Customer",
    EntityId = customer.Id.ToString(),
    UserId = customer.Id.ToString(),
    Timestamp = DateTime.UtcNow,
    Details = JsonSerializer.Serialize(new { RevokedSessions = revokedCount })
});
```

## Confidence

- High: custom JWT + EF session model is the right fit for this repo.
- High: admin and customer principals should stay separate.
- High: logout/revocation requires DB-backed session validation, not just short JWT expiry.
- Medium: customer auth should extend `Customer` rather than introduce `CustomerAccount`; this is the best repo fit, but depends on how much guest-vs-registered separation is desired later.
