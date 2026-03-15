---
estimated_steps: 4
estimated_files: 7
---

# T03: Add SuperAdmin admin-user management endpoints and RBAC matrix proofs

**Slice:** S04 — Admin Auth, RBAC & Password Reset Backend
**Milestone:** M001

## Description

Close AUTH-08 and remaining AUTH-09 by introducing real `SuperAdminOnly` management surfaces and explicit policy-matrix verification so role boundaries are enforced by code and tests, not assumptions.

## Steps

1. Implement `AdminUsersController` under `api/admin/v1/users` with `SuperAdminOnly` protection and endpoints for list/create/update-role/activate-deactivate (plus reset-initiation hook behavior defined in slice scope).
2. Add admin-user management contracts/DTOs that use role constants and validate supported roles (`Admin`, `SuperAdmin`) without casing drift.
3. Add controller tests for superadmin management behaviors, including role update and active-state transitions.
4. Add RBAC matrix tests (reflection + policy assertions) to verify required policy assignment across guest/customer/admin/superadmin protected endpoints.

## Must-Haves

- [ ] Management endpoints required for AUTH-08 exist and are protected by `SuperAdminOnly`.
- [ ] Role handling uses shared constants and rejects invalid role values deterministically.
- [ ] RBAC matrix tests cover Guest/Customer/Admin/SuperAdmin policy boundaries on key auth/admin endpoints.

## Verification

- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminUsersControllerTests|FullyQualifiedName~RbacPolicyMatrixTests|FullyQualifiedName~AuthConventionsTests"`
- Test assertions demonstrate `SuperAdminOnly` on admin-user management actions and preserve existing `AdminOnly`/`CustomerOnly` assignments on auth surfaces.

## Observability Impact

- Signals added/changed: admin-user role and active-state mutations become explicit API-managed transitions.
- How a future agent inspects this: run management + RBAC matrix tests; inspect role/active flags in in-memory `AdminUsers` set.
- Failure state exposed: policy drift or role-string drift fails deterministic tests before runtime.

## Inputs

- `backend/src/RentACar.API/Configuration/AuthPolicyNames.cs` — policy constants to enforce.
- `backend/src/RentACar.API/Authentication/AuthRoleNames.cs` — role constants and admin-role helper.
- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — existing admin-protected endpoints used in matrix checks.
- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs` — customer-protected endpoints used in matrix checks.
- `backend/tests/RentACar.Tests/Unit/Services/AuthConventionsTests.cs` — existing conventions test surface for extension.

## Expected Output

- `backend/src/RentACar.API/Controllers/AdminUsersController.cs` — superadmin management endpoints.
- `backend/src/RentACar.API/Contracts/Auth/AdminUserCreateRequest.cs` — create contract.
- `backend/src/RentACar.API/Contracts/Auth/AdminUserUpdateRoleRequest.cs` — role update contract.
- `backend/src/RentACar.API/Contracts/Auth/AdminUserDto.cs` — list/detail response shape.
- `backend/tests/RentACar.Tests/Unit/Controllers/AdminUsersControllerTests.cs` — behavior tests for management endpoints.
- `backend/tests/RentACar.Tests/Unit/Controllers/RbacPolicyMatrixTests.cs` — policy assignment/matrix closure tests.
- `backend/tests/RentACar.Tests/Unit/Services/AuthConventionsTests.cs` — role/policy convention extensions.
