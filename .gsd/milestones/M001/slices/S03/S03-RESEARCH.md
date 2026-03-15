# M001 / S03 (Customer Auth API) — Research

**Date:** 2026-03-15

## Summary

S03 primarily owns **AUTH-01, AUTH-02, AUTH-04, AUTH-05, AUTH-10** and directly supports **AUTH-03** (by consuming S02’s 15-minute token infrastructure) plus part of **AUTH-09** (customer-side role/policy enforcement). S02 already delivered strong primitives: shared JWT issuance (`sid`/`ver`), refresh-token hashing/replay helpers, DB-backed session validation, and secure refresh-cookie conventions. So S03 should focus on endpoint orchestration and principal state transitions, not crypto/auth plumbing.

Biggest surprise: the current `Customer` aggregate is not credential-ready. It has lockout/token-version fields but **no `PasswordHash`** in domain or schema, and customer records are currently created via reservation flow as profile/contact records. That means S03 cannot deliver AUTH-01/AUTH-02 cleanly without a schema/domain update plus clear rules for converting existing guest-created customer records into authenticated accounts.

Second surprise: current auth behavior still has drift risks even after S01/S02 hardening (exact-email lookup in existing auth flow, no lockout enforcement yet, logout currently cookie clear only unless session revocation is added in endpoint orchestration). S03 should explicitly close these behavior gaps for customer endpoints.

## Recommendation

Implement S03 in two layers:

1. **Customer auth contract/controller layer** (`register`, `login`, `refresh`, `logout`) reusing existing infrastructure:
   - `IJwtTokenService` for access/refresh primitives
   - `IRefreshTokenCookieService` for cookie write/clear
   - `AuthSession` persistence as source of truth
   - `ApiResponse<T>` + generic unauthorized responses

2. **Customer credential + state layer** before endpoint completion:
   - Add customer credential storage (likely `PasswordHash`, migration required)
   - Enforce normalized-email lookup (`NormalizedEmail`) for case-insensitive auth
   - Implement failed-login counting + 5-attempt lockout reset/unlock policy
   - Revoke/rotate session state on refresh/logout (not cookie-only)

Do **not** hand-roll token/cookie/security primitives already introduced in S02.

## Don’t Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|-------------------|------------|
| Access token creation (`sid`, `ver`, role/principal claims, 15-min expiry) | `IJwtTokenService` / `JwtTokenService` | Already aligned with D008 + test coverage; prevents claim drift. |
| Refresh token generation/hash/verification/replay checks | `JwtTokenService.CreateRefreshToken/HashRefreshToken/VerifyRefreshToken/IsRefreshTokenReplay` | Implements CSPRNG + hash-at-rest + constant-time verify from D009. |
| Session-authoritative token acceptance | `IAccessTokenSessionValidator` in JWT bearer `OnTokenValidated` | Preserves D010 behavior (signed token alone is insufficient). |
| Refresh cookie security settings | `IRefreshTokenCookieService` + `RefreshTokenCookieSettings` | Keeps HttpOnly/SameSite/Secure conventions centralized (D011). |
| Password hashing | `IPasswordHasher` (`BcryptPasswordHasher`) | Avoids inconsistent work factors/algorithms across endpoints. |

## Existing Code and Patterns

- `backend/src/RentACar.API/Services/JwtTokenService.cs` — canonical access/refresh token implementation; includes customer token creation and replay helper.
- `backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs` — DB session + token-version gate used post-JWT validation.
- `backend/src/RentACar.API/Authentication/RefreshTokenCookieService.cs` — single place for secure refresh-cookie append/clear behavior.
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — authoritative auth pipeline wiring (`OnTokenValidated`, `OnChallenge`, policies, rate limits).
- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — current auth-controller pattern for request validation, token issuance, session persistence, cookie handling, response shape.
- `backend/src/RentACar.Core/Entities/Customer.cs` — has lockout/token-version/normalized email, but no password credential field.
- `backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs` — unique `normalized_email` index and auth-state columns.
- `backend/src/RentACar.API/Services/ReservationService.cs` — currently creates/looks up customers by raw `Email` (case-sensitive) as reservation profile records.
- `backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs` — most reliable token contract/regression surface.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminAuthControllerTests.cs` — current controller-level pattern and known behavior expectations.

## Constraints

- **Credential gap:** `Customer` and `customers` table currently lack a password hash column; AUTH-01/AUTH-02 cannot be complete without schema/domain change.
- **Mixed customer lifecycle:** customers are auto-created from reservation requests; many records may predate auth intent.
- **Data invariants:** unique `normalized_email` index is already enforced; registration/login should use normalized lookup path.
- **Session truth:** JWT acceptance already depends on valid `AuthSession` + token version; refresh/logout must keep this state coherent.
- **Security contract:** unauthorized challenge intentionally returns generic `ApiResponse<object>`; avoid user enumeration.
- **Rate-limit policy:** strict policy exists (5/min); auth endpoints should use it consistently.
- **Cookie behavior differs by env:** production `SecurePolicy=Always`, development `SameAsRequest`.
- **No existing customer auth contracts/controller:** S03 starts from near-zero API surface for customer auth.

## Common Pitfalls

- **Case-sensitive email lookup** — querying by `Email` instead of `NormalizedEmail` causes false negatives and index bypass.
- **Cookie-only logout** — clearing cookie without revoking/expiring session leaves backend session valid.
- **Skipping replay checks on refresh** — not using `IsRefreshTokenReplay` weakens rotation defense.
- **Partial lockout implementation** — incrementing `FailedLoginCount` without atomic reset/unlock paths leads to inconsistent lockouts.
- **Treating reservation customer = verified account** — guest-created customer rows can create account-claim ambiguity if registration flow is not explicit.

## Open Risks

- Migration design for adding customer credentials may require handling existing reservation-created rows safely.
- Registration policy for pre-existing customer emails (claim/upgrade vs reject) is not yet defined.
- Refresh rotation model (same session update vs new session with `ReplacedBySessionId`) is not yet codified at endpoint level.
- SameSite Strict cookie behavior may complicate cross-origin frontend integration in some deployments.
- Test coverage gap: no customer auth controller tests currently exist.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Security/auth hardening | `security-best-practices` | **installed** |
| Library/API doc verification | `context7` | **installed** |
| ASP.NET Core API auth flows | `mindrally/skills@aspnet-core` | **available** — `npx skills add mindrally/skills@aspnet-core` |
| EF Core data/auth persistence | `github/awesome-copilot@ef-core` | **available** — `npx skills add github/awesome-copilot@ef-core` |
| PostgreSQL schema/index design | `wshobson/agents@postgresql-table-design` | **available** — `npx skills add wshobson/agents@postgresql-table-design` |
| BCrypt-specific skill | *(none found)* | **none found** (`npx skills find "BCrypt"`) |

## Sources

- Customer auth-state exists but no customer credential column (source: `backend/src/RentACar.Core/Entities/Customer.cs`, `backend/src/RentACar.Infrastructure/Data/Migrations/RentACarDbContextModelSnapshot.cs`).
- Reservation flow currently creates/queries customers as profile records using raw `Email` matching (source: `backend/src/RentACar.API/Services/ReservationService.cs`).
- Existing auth controller pattern for session creation, refresh cookie append/clear, and generic unauthorized behavior (source: `backend/src/RentACar.API/Controllers/AdminAuthController.cs`, `backend/src/RentACar.API/Controllers/BaseApiController.cs`).
- Session-authoritative JWT validation and auth policy wiring constraints (source: `backend/src/RentACar.API/Authentication/AccessTokenSessionValidator.cs`, `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs`).
- Refresh-cookie conventions and environment-specific secure policy behavior (source: `backend/src/RentACar.API/Authentication/RefreshTokenCookieService.cs`, `backend/src/RentACar.API/Options/RefreshTokenCookieSettings.cs`, `backend/src/RentACar.API/appsettings*.json`).
- Token/refresh claim and replay contracts validated by tests (source: `backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs`, `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs`).
- JWT bearer event customization guidance (source: [ASP.NET Core docs — Customize JWT Token Validation Event Handler](https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/security/authentication/identity-api-authorization/includes/identity-api-authorization3-7.md)).
