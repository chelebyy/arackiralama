---
id: T1
parent: S01
milestone: M001
provides:
  - Customer/Admin principal auth-state fields and email-normalization invariants in the domain model
key_files:
  - backend/src/RentACar.Core/Entities/Customer.cs
  - backend/src/RentACar.Core/Entities/AdminUser.cs
  - backend/tests/RentACar.Tests/Unit/Entities/PrincipalAuthStateTests.cs
  - .gsd/milestones/M001/slices/S01/S01-PLAN.md
key_decisions:
  - D004: principal entities auto-normalize email on assignment
patterns_established:
  - Keep `Email` (trimmed source form) and `NormalizedEmail` (uppercase invariant) synchronized in entity setters
observability_surfaces:
  - Unit tests asserting normalization/default auth-state semantics
  - Failure-path test asserting structured bad-request auth response surface remains inspectable
duration: 52m
verification_result: passed
completed_at: 2026-03-15T11:54:00+03:00
blocker_discovered: false
---

# T1: Extend customer and admin principals for auth state

**Extended both principal entities with auth-state fields and added domain-level email normalization invariants with passing verification checks.**

## What Happened

Implemented T1 by extending both `Customer` and `AdminUser` with:
- `NormalizedEmail`
- `FailedLoginCount`
- `LockoutEndUtc`
- `LastLoginAtUtc`
- `TokenVersion`

To enforce normalization safety at the domain boundary (before EF indexes/query changes in later tasks), I changed both entities so assigning `Email` trims input and automatically updates `NormalizedEmail` using `ToUpperInvariant()`.

I added new unit tests under `Unit/Entities` to verify:
- email trimming + normalization behavior
- secure default values for lockout/session-version fields
- blank-email normalization behavior

Pre-flight requirement fixes were also applied to the slice plan:
- added `## Observability / Diagnostics`
- added `## Verification` with an explicit failure-path diagnostic check

## Verification

Executed with fresh runs:

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests"`  
  Result: **Passed** (5/5)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithMissingCredentials_ReturnsBadRequest"`  
  Result: **Passed** (1/1)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~DbContextTests"`  
  Result: **Passed** (2/2)

Note: an initial parallel test run hit transient `CS2012` file-lock contention; rerunning sequentially produced clean passing evidence.

## Diagnostics

- Domain invariant inspection:
  - `Customer.Email` and `AdminUser.Email` setters now maintain `NormalizedEmail` automatically.
- Failure-surface inspection:
  - `AdminAuthControllerTests.Login_WithMissingCredentials_ReturnsBadRequest` confirms structured failure output is preserved for auth diagnostics.
- Slice observability contract:
  - `.gsd/milestones/M001/slices/S01/S01-PLAN.md` now defines runtime signals, inspection surfaces, failure visibility, and redaction constraints.

## Deviations

- The authoritative task-plan file referenced by dispatch (`.gsd/milestones/M001/slices/S01/tasks/T1-PLAN.md`) did not exist in the workspace, so execution followed the slice plan contract (`S01-PLAN.md`) and task statement directly.

## Known Issues

- None for T1 implementation.

## Files Created/Modified

- `backend/src/RentACar.Core/Entities/Customer.cs` — added auth-state fields and email normalization behavior.
- `backend/src/RentACar.Core/Entities/AdminUser.cs` — added auth-state fields and email normalization behavior.
- `backend/tests/RentACar.Tests/Unit/Entities/PrincipalAuthStateTests.cs` — added unit coverage for principal normalization/default auth-state semantics.
- `.gsd/milestones/M001/slices/S01/S01-PLAN.md` — added observability/diagnostics + verification sections; marked T1 complete.
- `.gsd/DECISIONS.md` — appended D004 decision about entity-level email normalization.
