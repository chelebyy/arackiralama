---
wave: 2
depends_on:
  - 01-auth-domain-persistence-PLAN.md
  - 02-token-session-infrastructure-PLAN.md
files_modified:
  - backend/src/RentACar.API/Controllers/AdminAuthController.cs
  - backend/src/RentACar.API/Controllers/AdminUsersController.cs
  - backend/src/RentACar.API/Contracts/AdminAuth/*
  - backend/src/RentACar.API/Contracts/AdminUsers/*
  - backend/src/RentACar.API/Services/AdminAuth/*
  - backend/src/RentACar.API/Services/AdminUsers/*
  - backend/tests/**/*Admin*Auth*
autonomous: true
requirements:
  - AUTH-05
  - AUTH-07
  - AUTH-08
  - AUTH-09
  - AUTH-10
---

# Plan 04: Admin Auth Hardening & Admin User Management

## Objective

Upgrade the existing admin auth path with refresh/logout revocation, lockout protection, case-insensitive login, and SuperAdmin-only admin-user management with critical audit logging.

## Tasks

<tasks>
  <task id="P6-04-T1" title="Refactor admin login to use the shared auth infrastructure">
    <files>
      <file>backend/src/RentACar.API/Controllers/AdminAuthController.cs</file>
      <file>backend/src/RentACar.API/Contracts/AdminAuth/*</file>
      <file>backend/src/RentACar.API/Services/AdminAuth/*</file>
    </files>
    <steps>
      <step>Switch admin login to normalized email lookup.</step>
      <step>Apply the same failed-attempt and lockout model as customer login, while still honoring inactive-admin hard denial.</step>
      <step>Add refresh and real logout/revocation behavior to the admin flow.</step>
    </steps>
  </task>
  <task id="P6-04-T2" title="Add SuperAdmin-only admin user management endpoints">
    <files>
      <file>backend/src/RentACar.API/Controllers/AdminUsersController.cs</file>
      <file>backend/src/RentACar.API/Contracts/AdminUsers/*</file>
      <file>backend/src/RentACar.API/Services/AdminUsers/*</file>
    </files>
    <steps>
      <step>Create endpoints for listing, creating, updating, activating/deactivating, and changing roles for admin users.</step>
      <step>Restrict creation and role changes to `SuperAdminOnly` exactly as decided in phase context.</step>
      <step>Terminate active sessions immediately when an admin is deactivated.</step>
    </steps>
  </task>
  <task id="P6-04-T3" title="Enforce and audit RBAC-sensitive operations">
    <files>
      <file>backend/src/RentACar.API/Services/AdminAuth/*</file>
      <file>backend/src/RentACar.API/Services/AdminUsers/*</file>
    </files>
    <steps>
      <step>Ensure role claims and policies cleanly distinguish Admin vs SuperAdmin access.</step>
      <step>Write audit log entries for admin login/logout, failed login lockouts, admin creation, role change, activation/deactivation, and session termination.</step>
      <step>Keep audit shapes aligned with existing `AuditLog` usage patterns in the repo.</step>
    </steps>
  </task>
</tasks>

## Must Haves

- Admin login is case-insensitive and lockout-aware.
- Admin logout is real revocation, not a no-op.
- Only SuperAdmin can create admins or change admin roles.
- Admin deactivation terminates active sessions immediately.
- Critical auth and admin-user events are audit logged.

## Verification Criteria

- `AdminAuthController` supports login, refresh, and logout with session-aware behavior.
- `AdminUsersController` exists with SuperAdmin-protected creation and role-management endpoints.
- Deactivated admins lose access before their access token naturally expires.
- Audit entries are emitted for the critical operations defined in phase context.
- Tests cover role restrictions, deactivation revocation, and case-insensitive email login.

