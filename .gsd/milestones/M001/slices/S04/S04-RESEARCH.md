# M001 / S04 (Admin Auth, RBAC & Password Reset Backend) — Research

**Date:** 2026-03-15

## Summary

S04 directly owns all currently **Active** auth requirements: **AUTH-06 (password reset via email link), AUTH-07 (admin login), AUTH-08 (superadmin manages admin users), AUTH-09 (RBAC closure across Guest/Customer/Admin/SuperAdmin)**. Based on codebase inspection, S03’s customer/session foundation is solid and reusable, but S04 is still largely unimplemented beyond a basic admin login/logout skeleton.

Biggest surprises during research:
- `PasswordResetToken` persistence is already modeled and migrated, but **no API/controller/service uses it yet**.
- Admin auth exists at `api/admin/v1/auth`, but it is **not session-lifecycle complete** (no refresh endpoint, logout does not revoke DB session, no lockout mutation).
- RBAC constants/policies exist, but **`SuperAdminOnly` is not applied anywhere** and there are **no admin-management endpoints**, leaving AUTH-08/09 incomplete.
- Admin login query uses `Email ==` (case-sensitive behavior), despite normalized email invariants in domain/schema.

A quick baseline verification passed for relevant existing tests (admin/customer/session primitives), confirming the starting point is stable before S04 changes:
- `AdminAuthControllerTests + AuthConventionsTests + AuthPersistenceModelsTests` (18/18 pass)
- `CustomerAuthControllerTests + AccessTokenSessionValidatorTests` (20/20 pass)

## Recommendation

Implement S04 as three tightly-coupled closures (in this order):

1. **Admin auth lifecycle parity with customer auth (AUTH-07 + part of AUTH-09)**
   - Normalize email lookup (`NormalizedEmail`) for admin login.
   - Enforce 5-fail/15-min lockout using existing admin auth-state fields.
   - Add admin refresh endpoint with S03-style rotation lineage (`revoked_at_utc`, `replaced_by_session_id`).
   - Change admin logout to revoke session server-side (not cookie-only).

2. **Password reset request/confirm flow with non-enumerating behavior (AUTH-06 + part of AUTH-09)**
   - Use `PasswordResetToken` hash-at-rest + single-use (`TryConsume`) semantics.
   - Implement request endpoint that always returns generic success/failure shape (no account enumeration).
   - Implement confirm endpoint that verifies token hash + expiry + consumed state, resets password, consumes token.
   - On successful reset, bump `TokenVersion` and revoke active sessions for the principal to invalidate prior access tokens.
   - Add an email dispatch abstraction/contract now (implementation can be minimal/stubbed but executable path must exist and be testable).

3. **SuperAdmin management + RBAC matrix closure (AUTH-08 + remaining AUTH-09)**
   - Add endpoints for superadmin admin-user management (create/list/update role/disable-reset-password path as scoped).
   - Protect these with `SuperAdminOnly` policy and add explicit role-policy matrix tests.
   - Keep unauthorized/forbidden contracts deterministic for S05 frontend guard integration.

## Don’t Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|-------------------|------------|
| JWT claims, 15-minute access token, `sid`/`ver` contract | `IJwtTokenService` / `JwtTokenService` | Already aligned with D008/D009/D011 and tested; avoids claim drift. |
| Refresh-token hash-at-rest + replay-safe comparison | `JwtTokenService.HashRefreshToken/VerifyRefreshToken/IsRefreshTokenReplay` | Preserves constant-time verification and no plaintext token storage. |
| Session authority checks for access tokens | `IAccessTokenSessionValidator` in JWT `OnTokenValidated` | Enforces DB session + token-version truth (not signature-only auth). |
| Refresh cookie security behavior | `IRefreshTokenCookieService` + `RefreshTokenCookieSettings` | Centralized HttpOnly/SameSite/Secure handling for both admin/customer paths. |
| Generic unauthorized API contract | `BaseApiController.UnauthorizedResponse` + JWT `OnChallenge` override | Prevents auth error-shape drift and enumeration leakage. |
| Password reset token persistence shape | `PasswordResetToken` + EF config/indexes | Already migrated with unique token-hash and principal indexes; no schema reinvention needed. |

## Existing Code and Patterns

- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — current admin login/me/logout baseline; currently missing refresh, lockout, session-revoking logout.
- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs` — canonical lifecycle reference for lockout, refresh rotation, replay-safe rejection, and server-side logout revocation.
- `backend/src/RentACar.Core/Entities/AdminUser.cs` — normalized email + failed-login/lockout/token-version state already available.
- `backend/src/RentACar.Core/Entities/PasswordResetToken.cs` — single-use token model via `TryConsume` and active/expired checks.
- `backend/src/RentACar.Infrastructure/Data/Configurations/PasswordResetTokenConfiguration.cs` — unique `token_hash` + principal/expires/consumed indexes.
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — JWT bearer events, challenge shape, and role policy wiring.
- `backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs` — authoritative session/token-version enforcement for both principal types.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs` — current admin behavior assertions (also exposes case-sensitive email mismatch behavior).
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs` — canonical tests for revoked/replaced/expired session denial.

## Constraints

- `SuperAdminOnly` policy is defined but currently unused by any controller action; AUTH-08 cannot pass without new endpoints.
- No email provider abstraction is currently registered in Core/Infrastructure; S04 must introduce dispatch contract surface.
- Test suite is controller/unit-heavy (no `WebApplicationFactory` integration harness yet), so S04 proof likely needs targeted controller/service tests unless harness is introduced.
- Existing admin logout only clears cookie; preserving S03 lineage expectations requires DB revocation semantics.
- Role/policy matching in ASP.NET Core is case-sensitive; role string drift (`Admin` vs `admin`) will silently break RBAC.
- Current docs/runbook examples are stale vs runtime contracts (`token` vs `accessToken`, refresh token in body vs cookie), so S04 should define authoritative contract for S05.

## Common Pitfalls

- **Case-sensitive admin identity lookup** — querying by `Email` instead of `NormalizedEmail` breaks legitimate logins and bypasses normalized index intent.
- **Cookie-only admin logout** — leaves server-side session active, enabling stale token acceptance windows.
- **Reset without global invalidation** — changing password without `TokenVersion` bump + session revocation allows old access tokens to survive.
- **Missing non-enumeration on forgot/reset** — exposing "user not found" leaks account existence.
- **RBAC by constants only** — defining policies without applying them to endpoints gives false confidence.

## Open Risks

- Scope ambiguity: whether AUTH-06 reset is customer-only or principal-agnostic (customer + admin) must be fixed before contract finalization.
- Dispatch strategy ambiguity: synchronous email send vs queued background job path for S04 contract proof before Phase 7 notifications.
- Potential migration need if S04 introduces additional reset metadata (e.g., requested IP/user agent) not currently in `PasswordResetToken`.
- S05 dependency risk if API contract names/shape change mid-slice without an explicit compatibility note.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Auth/RBAC security hardening | `security-best-practices` | **installed** |
| Library doc verification (ASP.NET/EF/JWT) | `context7` | **installed** |
| ASP.NET Core backend patterns | `mindrally/skills@aspnet-core` | **available** — `npx skills add mindrally/skills@aspnet-core` |
| EF Core modeling/migrations | `github/awesome-copilot@ef-core` | **available** — `npx skills add github/awesome-copilot@ef-core` |
| JWT/auth implementation patterns | `pluginagentmarketplace/custom-plugin-nodejs@jwt-authentication` | **available** — `npx skills add pluginagentmarketplace/custom-plugin-nodejs@jwt-authentication` |
| RBAC implementation patterns | `aj-geddes/useful-ai-prompts@access-control-rbac` | **available** — `npx skills add aj-geddes/useful-ai-prompts@access-control-rbac` |
| SMTP/email integration for .NET workflows | `aaronontheweb/dotnet-skills@mailpit-integration` | **available** — `npx skills add aaronontheweb/dotnet-skills@mailpit-integration` |
| BCrypt-specific backend auth skill | *(none strongly relevant found)* | **none found** (`npx skills find "BCrypt .NET"`) |

## Sources

- Admin auth baseline and gaps (source: `backend/src/RentACar.API/Controllers/AdminAuthController.cs`).
- Customer auth lifecycle reference (source: `backend/src/RentACar.API/Controllers/CustomerAuthController.cs`).
- Session validation and token-version authority checks (source: `backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs`).
- Role/policy wiring and unauthorized challenge contract (source: `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs`, `backend/src/RentACar.API/Controllers/BaseApiController.cs`).
- Password reset token domain + schema readiness (source: `backend/src/RentACar.Core/Entities/PasswordResetToken.cs`, `backend/src/RentACar.Infrastructure/Data/Configurations/PasswordResetTokenConfiguration.cs`, `backend/src/RentACar.Infrastructure/Data/Migrations/20260315090214_Phase6AuthDomainPersistenceFoundation.cs`).
- Current RBAC application surface (source: `backend/src/RentACar.API/Controllers/Admin*.cs`, grep for `AdminOnly`/`SuperAdminOnly`).
- Admin/customer auth test baselines (source: `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs`, `backend/tests/RentACar.Tests/Unit/Controllers/CustomerAuthControllerTests.cs`, `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs`).
- ASP.NET Core policy role matching and `RequireRole` behavior (source: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles).
- ASP.NET Core JWT bearer event customization references (source: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization_view=aspnetcore-1.0).
