# S02: JWT & Session Infrastructure — UAT

**Milestone:** M001
**Written:** 2026-03-15

## UAT Type

- UAT mode: mixed (artifact-driven + live-runtime)
- Why this mode is sufficient: This slice is infrastructure-heavy (token/session/cookie/auth-pipeline behavior). Deterministic automated tests prove contract-level correctness; focused runtime checks validate HTTP surfaces (`Set-Cookie`, 401 challenge body) exposed to clients.

## Preconditions

- From repo root (`C:\All_Project\Arac-Kiralama`), backend dependencies restored.
- Test database/in-memory test setup available for `RentACar.Tests`.
- API configuration contains JWT settings (`AccessTokenMinutes=15`, `RefreshTokenDays=7`) and `Auth:RefreshTokenCookie` section.
- Admin test credentials/fixtures used by `AdminAuthControllerTests` are valid.

## Smoke Test

1. Run:
   - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithValidCredentials_ReturnsOk"`
2. **Expected:** 1/1 passing; login flow returns success path with session-backed token issuance behavior and refresh-cookie write hook.

## Test Cases

### 1. Access token contract includes `sid`/`ver` and 15-minute lifetime

1. Run:
   - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~JwtTokenServiceTests"`
2. Inspect passing assertions for claim contract.
3. **Expected:** tests confirm required claims (`sid`, `ver`, `principal_type`, `sub`, `email`), issuer/audience integrity, admin role/permission compatibility, and 15-minute expiry window.

### 2. Refresh-token primitives enforce 7-day expiry + hash/verify semantics

1. Run:
   - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~JwtTokenServiceTests"`
2. Confirm refresh-specific tests are included in run output.
3. **Expected:** refresh tokens are unique opaque values, expiry aligns with 7-day policy, stored hash format is `sha256:<64 hex>`, constant-time verify path succeeds for valid token/hash, and rotated-out token replay predicate is detected.

### 3. App-level session validation rejects invalid session state post-JWT validation

1. Run:
   - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AccessTokenSessionValidatorTests"`
2. Review branch coverage outcomes.
3. **Expected:** validator accepts only when claims/session/version align; rejects missing claims, revoked session, expired session, missing session, session principal mismatch, and token-version mismatch.

### 4. Admin login persists auth session and writes secure-ready refresh cookie

1. Run:
   - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithValidCredentials_ReturnsOk|FullyQualifiedName~RefreshTokenCookieServiceTests"`
2. Verify all tests pass.
3. **Expected:** successful login creates/persists `AuthSession`-compatible metadata with hashed refresh token and triggers cookie service write with HttpOnly + SameSite + secure policy conventions.

### 5. Unauthorized auth failures return structured non-leaky payload

1. Run:
   - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithInvalidCredentials_ReturnsUnauthorized"`
2. (Optional live check) Call an `[Authorize]` endpoint with invalid/missing bearer token.
3. **Expected:** unauthorized result is structured `ApiResponse<object>` with message `Yetkisiz erişim`; response does not leak principal/session internals.

## Edge Cases

### Session revoked after token issuance

1. Use test path in `AccessTokenSessionValidatorTests` that marks session revoked.
2. **Expected:** token is rejected even if JWT signature and lifetime are valid.

### Token version incremented (stale token)

1. Use test path in `AccessTokenSessionValidatorTests` where DB `TokenVersion` > token `ver`.
2. **Expected:** stale token rejected as unauthorized.

### Rotated refresh token replay

1. Use `JwtTokenServiceTests` replay scenario with rotated-out token and active hash.
2. **Expected:** replay predicate returns true, enabling endpoint-level revocation/escalation behavior.

### Logout cookie deletion semantics

1. Run `RefreshTokenCookieServiceTests` case for clear/remove behavior.
2. **Expected:** refresh cookie is cleared with matching path/domain attributes required for browser deletion.

## Failure Signals

- Any failure in required slice checks:
  - `JwtTokenServiceTests`
  - `AdminAuthControllerTests.Login_WithValidCredentials_ReturnsOk`
  - `AdminAuthControllerTests.Login_WithInvalidCredentials_ReturnsUnauthorized`
- Missing `sid`/`ver` claims in issued token payload.
- 401 responses returning default/plain challenge body instead of structured `ApiResponse<object>`.
- Refresh-cookie missing `HttpOnly` and expected secure/samesite conventions.
- Session-revoked or version-mismatch tokens still being accepted.

## Requirements Proved By This UAT

- AUTH-03 — JWT access-token 15-minute lifetime and required claim contract are verified at implementation/test boundary.
- AUTH-04 — 7-day refresh-token generation/hash/verify/replay primitives are verified.
- AUTH-07 — Admin login path with session-backed token issuance and refresh-cookie behavior is verified.
- AUTH-09 — Role/claim/policy convention matrix and admin-policy compatibility are verified.

## Not Proven By This UAT

- AUTH-01 / AUTH-02 — Customer register/login endpoints (S03 scope).
- AUTH-05 end-to-end customer logout/revocation journey via public API endpoints.
- AUTH-06 password reset email delivery.
- AUTH-08 superadmin management workflows.
- Browser-level frontend consumption of refresh cookie (S05 scope).

## Notes for Tester

- This slice intentionally focuses on infra contracts and security invariants. Do not expect full customer auth endpoint coverage yet.
- Treat token/session tests as authoritative for this slice; they are the fastest regression signal before S03 wiring.
- Never log raw JWT/refresh tokens in diagnostics during manual checks.