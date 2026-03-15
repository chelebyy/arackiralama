# S01: Auth Domain & Persistence Foundation — UAT

**Milestone:** M001
**Written:** 2026-03-15

## UAT Type

- UAT mode: **mixed** (artifact-driven + live-runtime verification)
- Why this mode is sufficient: S01 is a persistence/domain slice; correctness is proven through deterministic tests, EF model metadata, and generated migration SQL rather than UI flows.

## Preconditions

- Repository at `C:\All_Project\Arac-Kiralama` with S01 changes present.
- .NET 10 SDK and EF tooling available.
- Test project restore/build succeeds.
- Tester can run commands from repo root (or `backend/` as noted).

## Smoke Test

1. Run:
   - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests"`
2. **Expected:** all 5 tests pass, confirming principal auth-state fields and normalization invariants are active.

## Test Cases

### 1. Principal normalization and auth-state defaults are enforced

1. Run:
   - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests"`
2. Confirm tests cover both `Customer` and `AdminUser` email assignment and default auth-state values.
3. **Expected:** pass result with no failures; `Email` + `NormalizedEmail` behavior is consistent and counters/timestamps/token-version defaults are secure.

### 2. Auth persistence entities enforce hash-at-rest and lifecycle semantics

1. Run:
   - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AuthPersistenceModelsTests"`
2. Review test names/output for revoked/expired/consumed transitions and plaintext token property guards.
3. **Expected:** pass result; `AuthSession`/`PasswordResetToken` behave correctly for active/inactive states and expose no plaintext token members.

### 3. EF model maps auth columns/tables/indexes with snake_case and uniqueness constraints

1. Run:
   - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~DbContextTests"`
2. Confirm metadata assertions include `normalized_email`, lockout fields, token version, and new auth tables/indexes.
3. **Expected:** pass result (4/4) with no mapping/index failures.

### 4. Failure-path diagnostics remain structured for auth endpoints

1. Run:
   - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithMissingCredentials_ReturnsBadRequest"`
2. Inspect output for single passing test.
3. **Expected:** pass result; missing-credential login path still returns structured bad-request surface (no regression from persistence changes).

### 5. Migration SQL contains safe normalized-email rollout guards and auth tables

1. Generate SQL:
   - `cd backend`
   - `dotnet ef migrations script --project src/RentACar.Infrastructure --startup-project src/RentACar.API > /tmp/s01_migration.sql`
2. Search SQL for required markers:
   - `UPPER(TRIM(email))`
   - `RAISE EXCEPTION`
   - `ux_customers_normalized_email`
   - `ux_admin_users_normalized_email`
   - `CREATE TABLE auth_sessions`
   - `CREATE TABLE password_reset_tokens`
3. **Expected:** all markers exist, proving backfill + duplicate fail-fast + final uniqueness/table creation sequence.

## Edge Cases

### Duplicate normalized emails in existing data

1. Review generated migration SQL for explicit duplicate checks and hints before unique index creation.
2. **Expected:** migration includes `RAISE EXCEPTION` blocks with actionable preflight queries for both `customers` and `admin_users`.

### Token lifecycle transitions

1. Execute `AuthPersistenceModelsTests` and inspect cases for revoked/expired sessions and consumed/expired reset tokens.
2. **Expected:** inactive states are correctly reported; consumed reset token cannot be consumed again.

## Failure Signals

- Any failure in `DbContextTests` involving missing `normalized_email`, wrong column names, or absent indexes.
- Migration SQL missing backfill, duplicate guards, or auth table/index creation markers.
- Presence of plaintext token fields/properties in auth entities.
- Regression in missing-credentials auth failure-path test.

## Requirements Proved By This UAT

- **AUTH-04 (partial prerequisite)** — refresh-token persistence structures are present (not full flow validation).
- **AUTH-05 (partial prerequisite)** — revocation/logout-all persistence fields are present.
- **AUTH-06 (partial prerequisite)** — password-reset token persistence/lifecycle structures are present.
- **AUTH-07 (partial prerequisite)** — admin principal auth-state and normalization-safe persistence are present.
- **AUTH-10 (partial prerequisite)** — lockout state fields are persisted for enforcement in later slices.

## Not Proven By This UAT

- End-to-end register/login API behavior (`AUTH-01`, `AUTH-02`, `AUTH-07` full validation).
- JWT issuance/expiry (`AUTH-03`) and refresh flow runtime behavior (`AUTH-04` full validation).
- Logout API behavior and distributed/session invalidation runtime semantics (`AUTH-05` full validation).
- Password reset email delivery and full reset UX (`AUTH-06` full validation).

## Notes for Tester

- This slice is intentionally infrastructure-first; successful UAT means the persistence contract is trustworthy for S02+ implementation.
- If migration duplicate guard exceptions appear on deployment, run the SQL hints embedded in the exception text to clean conflicting historical rows before retrying migration.
