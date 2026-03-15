---
id: T03
parent: S04
milestone: M001
provides:
  - SuperAdmin-only admin-user management endpoints (list/create/update-role/activate/deactivate/reset-initiation) with deterministic role validation and explicit RBAC policy-matrix proofs.
key_files:
  - backend/src/RentACar.API/Controllers/AdminUsersController.cs
  - backend/src/RentACar.API/Contracts/Auth/AdminUserCreateRequest.cs
  - backend/src/RentACar.API/Contracts/Auth/AdminUserUpdateRoleRequest.cs
  - backend/src/RentACar.API/Contracts/Auth/AdminUserDto.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/AdminUsersControllerTests.cs
  - backend/tests/RentACar.Tests/Unit/Controllers/RbacPolicyMatrixTests.cs
  - backend/tests/RentACar.Tests/Unit/Services/AuthConventionsTests.cs
  - backend/src/RentACar.API/Authentication/AuthRoleNames.cs
key_decisions:
  - D020: Canonical admin-role normalization now flows through `AuthRoleNames.TryNormalizeAdminRole`, and SuperAdmin reset-initiation reuses hash-at-rest reset token + dispatch contract patterns.
patterns_established:
  - Admin-user create/update-role paths accept only canonical `Admin`/`SuperAdmin` values (case-drift rejected deterministically).
  - RBAC closure is enforced via reflection-based effective-policy assertions across anonymous/customer/admin/superadmin endpoint surfaces.
observability_surfaces:
  - `AdminUsersControllerTests` verifies persisted `AdminUsers.role`/`AdminUsers.is_active` transitions and reset-trigger token persistence in `PasswordResetTokens` plus dispatcher invocation.
  - `RbacPolicyMatrixTests` exposes policy drift quickly by failing on missing/misaligned `Authorize`/`AllowAnonymous` attributes.
  - Verification command: `dotnet test ... --filter "FullyQualifiedName~AdminUsersControllerTests|FullyQualifiedName~RbacPolicyMatrixTests|FullyQualifiedName~AuthConventionsTests"`.
duration: 1h20m
verification_result: passed
completed_at: 2026-03-15
blocker_discovered: false
---

# T03: Add SuperAdmin admin-user management endpoints and RBAC matrix proofs

**Added a real `SuperAdminOnly` admin-user management surface plus deterministic role/policy tests that close AUTH-08 and backend AUTH-09 scope for S04.**

## What Happened

Implemented `AdminUsersController` at `api/admin/v1/users` with `SuperAdminOnly` protection and endpoints for:
- list admins (`GET /api/admin/v1/users`)
- create admin (`POST /api/admin/v1/users`)
- update role (`PUT /api/admin/v1/users/{id}/role`)
- activate (`POST /api/admin/v1/users/{id}/activate`)
- deactivate (`POST /api/admin/v1/users/{id}/deactivate`)
- password-reset initiation hook (`POST /api/admin/v1/users/{id}/reset-password`)

Added contracts/DTOs (`AdminUserCreateRequest`, `AdminUserUpdateRoleRequest`, `AdminUserDto`) and centralized canonical role parsing in `AuthRoleNames.TryNormalizeAdminRole` so only `Admin`/`SuperAdmin` are accepted (case-drift values like `admin` and `SUPERADMIN` are rejected).

Added `AdminUsersControllerTests` to prove behavior (role updates, active-state transitions, reset-token creation+dispatch) and `RbacPolicyMatrixTests` to lock policy coverage across guest/customer/admin/superadmin endpoint surfaces.

Extended `AuthConventionsTests` to verify canonical role normalization and deterministic rejection behavior.

## Verification

Executed task verification command:
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminUsersControllerTests|FullyQualifiedName~RbacPolicyMatrixTests|FullyQualifiedName~AuthConventionsTests"`
- Result: **Passed** (45/45)

Executed full S04 slice verification set:
- `dotnet test ... --filter "FullyQualifiedName~AdminAuthControllerTests|FullyQualifiedName~AccessTokenSessionValidatorTests"` → **Passed** (19/19)
- `dotnet test ... --filter "FullyQualifiedName~PasswordResetControllerTests"` → **Passed** (7/7)
- `dotnet test ... --filter "FullyQualifiedName~AdminUsersControllerTests|FullyQualifiedName~RbacPolicyMatrixTests|FullyQualifiedName~AuthConventionsTests"` → **Passed** (45/45)

## Diagnostics

For future inspection:
- Run `AdminUsersControllerTests` to verify persisted `AdminUsers` role/active mutations and `PasswordResetTokens` creation + dispatch-invocation path.
- Run `RbacPolicyMatrixTests` to detect policy drift on key auth/admin endpoints (`AllowAnonymous`, `CustomerOnly`, `AdminOnly`, `SuperAdminOnly`).
- In-memory failure signatures are deterministic (invalid role => bad request; missing user => not found; inactive reset target => bad request).

## Deviations

- None.

## Known Issues

- None discovered in scope.

## Files Created/Modified

- `backend/src/RentACar.API/Controllers/AdminUsersController.cs` — new superadmin admin-user management endpoints + reset-initiation hook.
- `backend/src/RentACar.API/Contracts/Auth/AdminUserCreateRequest.cs` — create contract.
- `backend/src/RentACar.API/Contracts/Auth/AdminUserUpdateRoleRequest.cs` — role-update contract.
- `backend/src/RentACar.API/Contracts/Auth/AdminUserDto.cs` — response DTO for list/detail payloads.
- `backend/src/RentACar.API/Authentication/AuthRoleNames.cs` — added canonical `TryNormalizeAdminRole` helper and reused it in `IsAdminRole`.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminUsersControllerTests.cs` — behavior tests for create/list/update-role/activate/deactivate/reset-initiation.
- `backend/tests/RentACar.Tests/Unit/Controllers/RbacPolicyMatrixTests.cs` — reflection-based RBAC policy matrix assertions.
- `backend/tests/RentACar.Tests/Unit/Services/AuthConventionsTests.cs` — extended conventions coverage for canonical role normalization and casing drift rejection.
- `.gsd/DECISIONS.md` — appended D020.
- `.gsd/milestones/M001/slices/S04/S04-PLAN.md` — marked T03 as complete.
