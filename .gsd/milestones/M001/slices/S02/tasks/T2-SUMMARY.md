---
id: T2
parent: S02
milestone: M001
provides:
  - Cryptographically secure refresh-token generation plus hash/verify/replay helpers with 7-day expiry semantics for session rotation flows
key_files:
  - backend/src/RentACar.API/Services/IJwtTokenService.cs
  - backend/src/RentACar.API/Services/JwtTokenService.cs
  - backend/src/RentACar.API/Options/JwtOptions.cs
  - backend/src/RentACar.API/appsettings.json
  - backend/src/RentACar.API/appsettings.Development.json
  - backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs
  - .gsd/milestones/M001/slices/S02/S02-PLAN.md
  - .gsd/DECISIONS.md
  - .gsd/STATE.md
key_decisions:
  - D009: refresh tokens use 64-byte CSPRNG Base64Url values, `sha256:` hash-at-rest, and constant-time verification helpers
patterns_established:
  - Refresh/session flows should use `CreateRefreshToken` + `HashRefreshToken` on issue, `VerifyRefreshToken` on validation, and `IsRefreshTokenReplay` to flag rotated-out token reuse
observability_surfaces:
  - `JwtTokenServiceTests` now deterministically validate refresh-token expiry policy, hash format, constant-time validation path, and replay detection behavior
duration: 43m
verification_result: passed
completed_at: 2026-03-15T12:26:05+0300
# Set blocker_discovered: true only if execution revealed the remaining slice plan
# is fundamentally invalid (wrong API, missing capability, architectural mismatch).
# Do NOT set true for ordinary bugs, minor deviations, or fixable issues.
blocker_discovered: false
---

# T2: Add refresh-token generation and hashing helpers

**Added production refresh-token primitives to `JwtTokenService` (generation, SHA-256 hash-at-rest, constant-time verification, and replay predicate) with 7-day expiry support and unit-test coverage.**

## What Happened

Implemented T2 by extending the JWT service contract and implementation with refresh-token helpers:

- Expanded `IJwtTokenService` with:
  - `CreateRefreshToken(out DateTime expiresAtUtc)`
  - `HashRefreshToken(string refreshToken)`
  - `VerifyRefreshToken(string refreshToken, string refreshTokenHash)`
  - `IsRefreshTokenReplay(string refreshToken, string activeRefreshTokenHash)`
- Updated `JwtTokenService` to:
  - generate opaque refresh tokens via `RandomNumberGenerator.GetBytes(64)` + Base64Url encoding
  - enforce 7-day refresh-token expiry (`JwtOptions.RefreshTokenDays`, default/fallback = 7)
  - hash tokens as `sha256:<lowercase-hex>` (no plaintext persistence helper)
  - compare hashes with `CryptographicOperations.FixedTimeEquals` to avoid timing leaks
  - expose replay detection helper as a thin predicate over hash verification (for rotated-out token rejection paths)
- Extended `JwtOptions` and both appsettings files with `RefreshTokenDays: 7`.
- Added/updated unit tests in `JwtTokenServiceTests` for:
  - refresh token uniqueness + 7-day expiry window
  - hash format (`sha256:` + 64 hex)
  - positive verify path
  - replay detection using rotated-out token vs active hash
  - invalid-input guardrail (empty token hashing throws)

No plaintext refresh token/hash logging surfaces were introduced.

## Verification

Slice verification commands from `S02-PLAN.md` were executed and all passed:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~JwtTokenServiceTests"`  
  Result: **Passed** (14/14)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithValidCredentials_ReturnsOk"`  
  Result: **Passed** (1/1)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithInvalidCredentials_ReturnsUnauthorized"`  
  Result: **Passed** (1/1)

## Diagnostics

To inspect T2 behavior later:

- Run `JwtTokenServiceTests` and focus on refresh helpers:
  - `CreateRefreshToken_WithDefaultOptions_ReturnsUniqueOpaqueTokenAnd7DayExpiry`
  - `VerifyRefreshToken_WithMatchingTokenAndHash_ReturnsTrue`
  - `IsRefreshTokenReplay_WhenUsingRotatedOutToken_ReturnsTrue`
- For runtime inspection during future refresh endpoint wiring:
  - persist only `HashRefreshToken(token)` output (`sha256:` prefixed)
  - validate incoming token with `VerifyRefreshToken`
  - treat `IsRefreshTokenReplay == true` as rotated/replay candidate signal (combined with session revocation state)

## Deviations

- Dispatch-required task plan file `.gsd/milestones/M001/slices/S02/tasks/T2-PLAN.md` was not present in workspace; execution followed the authoritative slice contract (`S02-PLAN.md`) and dispatch scope for T2.

## Known Issues

- None for T2 scope.

## Files Created/Modified

- `backend/src/RentACar.API/Services/IJwtTokenService.cs` — added refresh-token helper contract methods.
- `backend/src/RentACar.API/Services/JwtTokenService.cs` — implemented secure refresh generation, hashing, constant-time validation, and replay predicate.
- `backend/src/RentACar.API/Options/JwtOptions.cs` — added configurable `RefreshTokenDays` (default 7).
- `backend/src/RentACar.API/appsettings.json` — added `Jwt.RefreshTokenDays`.
- `backend/src/RentACar.API/appsettings.Development.json` — added `Jwt.RefreshTokenDays`.
- `backend/tests/RentACar.Tests/Unit/Services/JwtTokenServiceTests.cs` — added refresh helper coverage and replay/rotation semantics checks.
- `.gsd/milestones/M001/slices/S02/S02-PLAN.md` — marked T2 complete.
- `.gsd/DECISIONS.md` — appended D009 refresh-token infrastructure decision.
- `.gsd/STATE.md` — advanced active state to next task after T2.
