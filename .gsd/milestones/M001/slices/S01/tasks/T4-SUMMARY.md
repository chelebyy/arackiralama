---
id: T4
parent: S01
milestone: M001
provides:
  - EF Core migration + snapshot for principal auth-state columns, auth session/reset tables, and safe normalized-email rollout guards
key_files:
  - backend/src/RentACar.Infrastructure/Data/Migrations/20260315090214_Phase6AuthDomainPersistenceFoundation.cs
  - backend/src/RentACar.Infrastructure/Data/Migrations/20260315090214_Phase6AuthDomainPersistenceFoundation.Designer.cs
  - backend/src/RentACar.Infrastructure/Data/Migrations/RentACarDbContextModelSnapshot.cs
  - .gsd/milestones/M001/slices/S01/S01-PLAN.md
  - .gsd/DECISIONS.md
  - .gsd/STATE.md
key_decisions:
  - D007: migration uses backfill + duplicate-detection fail-fast before unique normalized-email constraints
patterns_established:
  - For auth identity hardening migrations: add nullable normalized column → backfill from canonical source (`UPPER(TRIM(email))`) → fail fast on duplicates with actionable SQL hint → enforce NOT NULL + unique index
observability_surfaces:
  - Migration SQL includes explicit duplicate-detection error messages/hints for customer/admin normalized email collisions
  - Model snapshot exposes auth-state fields and auth tables (`auth_sessions`, `password_reset_tokens`) for inspection
  - Slice verification tests and failure-path auth test remain passing
duration: 36m
verification_result: passed
completed_at: 2026-03-15T12:10:00+03:00
# Set blocker_discovered: true only if execution revealed the remaining slice plan
# is fundamentally invalid (wrong API, missing capability, architectural mismatch).
# Do NOT set true for ordinary bugs, minor deviations, or fixable issues.
blocker_discovered: false
---

# T4: Create migration with safe rollout notes

**Generated the auth foundation migration and hardened rollout safety by backfilling normalized emails and failing early on duplicate normalized principals before unique-index enforcement.**

## What Happened

Created migration `20260315090214_Phase6AuthDomainPersistenceFoundation` from the T1–T3 model changes, then adjusted the generated `Up` path to avoid unsafe default-empty normalized emails on existing rows.

Implemented safe rollout behavior directly in the migration:
- Added `normalized_email` to `customers` and `admin_users` as nullable first.
- Backfilled both tables via `UPPER(TRIM(email))`.
- Added duplicate-detection guards (`DO $$ ... RAISE EXCEPTION ... $$`) for customer/admin normalized-email collisions with actionable preflight SQL hints in the exception text.
- Altered normalized-email columns to `NOT NULL` only after backfill + duplicate checks.
- Kept unique normalized-email indexes and new auth persistence tables/indexes (`auth_sessions`, `password_reset_tokens`) as required by the slice contract.

Also updated slice/task tracking artifacts:
- marked T4 complete in `S01-PLAN.md`
- appended D007 in `.gsd/DECISIONS.md`
- refreshed `.gsd/STATE.md`

## Verification

Executed slice verification commands (all passed):

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests"`  
  Result: **Passed** (5/5)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithMissingCredentials_ReturnsBadRequest"`  
  Result: **Passed** (1/1)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~DbContextTests"`  
  Result: **Passed** (4/4)

Migration generation + schema evidence:
- `dotnet ef migrations add Phase6AuthDomainPersistenceFoundation --project src/RentACar.Infrastructure --startup-project src/RentACar.API` (succeeded)
- `dotnet ef migrations script --project src/RentACar.Infrastructure --startup-project src/RentACar.API` output verified to contain:
  - normalized-email backfill SQL
  - duplicate guard exceptions/hints
  - `ALTER ... normalized_email SET NOT NULL`
  - creation of `auth_sessions` and `password_reset_tokens`
  - unique indexes `ux_customers_normalized_email` and `ux_admin_users_normalized_email`
- Snapshot inspection confirms runtime-signal fields (`normalized_email`, `failed_login_count`, `lockout_end_utc`, `last_login_at_utc`, `token_version`) and both auth tables exist in model metadata.

## Diagnostics

Safe-rollout diagnostics now live in migration SQL itself:
- If duplicate normalized customer/admin emails exist, migration throws explicit exceptions with detailed preflight query hints.
- This surfaces guest-created customer collision risks before index creation, rather than failing with less-actionable unique-index errors.

Inspection surfaces:
- Migration file: `backend/src/RentACar.Infrastructure/Data/Migrations/20260315090214_Phase6AuthDomainPersistenceFoundation.cs`
- Snapshot: `backend/src/RentACar.Infrastructure/Data/Migrations/RentACarDbContextModelSnapshot.cs`
- SQL diff: `dotnet ef migrations script --project src/RentACar.Infrastructure --startup-project src/RentACar.API`

## Deviations

- The authoritative task-plan file referenced by dispatch (`.gsd/milestones/M001/slices/S01/tasks/T4-PLAN.md`) was not present in the workspace, so execution followed the slice contract in `S01-PLAN.md` plus the dispatch requirements.

## Known Issues

- EF tooling warning observed: local `dotnet-ef` tool version (`10.0.0-rc.2...`) is older than runtime (`10.0.3`). Migration/tests succeeded; no functional blocker for this task.

## Files Created/Modified

- `backend/src/RentACar.Infrastructure/Data/Migrations/20260315090214_Phase6AuthDomainPersistenceFoundation.cs` — generated migration, then hardened with backfill + duplicate guard + not-null enforcement sequence.
- `backend/src/RentACar.Infrastructure/Data/Migrations/20260315090214_Phase6AuthDomainPersistenceFoundation.Designer.cs` — EF-generated migration metadata.
- `backend/src/RentACar.Infrastructure/Data/Migrations/RentACarDbContextModelSnapshot.cs` — updated snapshot for auth-state columns and auth persistence tables/indexes.
- `.gsd/milestones/M001/slices/S01/S01-PLAN.md` — marked T4 complete.
- `.gsd/DECISIONS.md` — appended D007 rollout-safety migration decision.
- `.gsd/STATE.md` — updated active state/next action after T4.
