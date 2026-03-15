---
wave: 3
depends_on:
  - 03-customer-auth-api-PLAN.md
  - 04-admin-auth-rbac-PLAN.md
files_modified:
  - frontend/app/(admin)/dashboard/(guest)/login/**
  - frontend/app/(admin)/dashboard/(guest)/register/**
  - frontend/app/(admin)/dashboard/(guest)/forgot-password/**
  - frontend/lib/auth/**
  - frontend/middleware.ts
  - frontend/tests/**/*auth*
  - backend/tests/**/*auth*
autonomous: true
requirements:
  - AUTH-01
  - AUTH-02
  - AUTH-03
  - AUTH-04
  - AUTH-05
  - AUTH-06
  - AUTH-07
  - AUTH-08
  - AUTH-09
  - AUTH-10
---

# Plan 05: Frontend Auth Integration & Phase Verification

## Objective

Connect the new backend auth flows to the existing frontend auth pages, establish session bootstrap/logout behavior, and verify that all Phase 6 requirements are covered by tests and end-to-end user flows.

## Tasks

<tasks>
  <task id="P6-05-T1" title="Build frontend auth client and route guards">
    <files>
      <file>frontend/lib/auth/**</file>
      <file>frontend/middleware.ts</file>
    </files>
    <steps>
      <step>Create a small auth client for login, register, refresh, logout, forgot-password, reset-password, and current-user bootstrap.</step>
      <step>Support cookie-based refresh and silent session refresh while the user is active.</step>
      <step>Add route protection/redirect behavior for admin guest vs authenticated dashboard access.</step>
    </steps>
  </task>
  <task id="P6-05-T2" title="Wire existing auth page templates to real flows">
    <files>
      <file>frontend/app/(admin)/dashboard/(guest)/login/**</file>
      <file>frontend/app/(admin)/dashboard/(guest)/register/**</file>
      <file>frontend/app/(admin)/dashboard/(guest)/forgot-password/**</file>
    </files>
    <steps>
      <step>Reuse the existing login/register/forgot-password templates instead of inventing a new auth UI system.</step>
      <step>Hook form submission, validation feedback, loading state, and success/error UX to the real API contracts.</step>
      <step>Make sure logout and expired-session behavior are reflected in the UI.</step>
    </steps>
  </task>
  <task id="P6-05-T3" title="Add requirement-level verification coverage">
    <files>
      <file>frontend/tests/**/*auth*</file>
      <file>backend/tests/**/*auth*</file>
    </files>
    <steps>
      <step>Add backend tests where still missing for registration, login, JWT claims, refresh rotation, logout revocation, password reset, admin login, admin management, RBAC, and lockout.</step>
      <step>Add frontend or E2E tests that validate the key user-visible auth paths and admin guest-page integration.</step>
      <step>Create a simple requirement trace note in test naming or comments so execute-phase verification can map coverage back to AUTH-01 through AUTH-10.</step>
    </steps>
  </task>
</tasks>

## Must Haves

- Existing frontend auth pages are reused, not replaced wholesale.
- Silent refresh is active-session aware and based on secure refresh-cookie flow.
- Admin guest routes redirect cleanly after login/logout and on unauthorized access.
- Verification evidence exists for every Phase 6 auth requirement.

## Verification Criteria

- Frontend can successfully call register/login/forgot-password/reset/logout flows against the real API.
- Session bootstrap restores authenticated state without storing refresh tokens in browser storage.
- Automated tests or E2E flows cover all AUTH-01 through AUTH-10 requirements.
- Execute-phase can run this plan last and use it as the final coverage gate for Phase 6.

