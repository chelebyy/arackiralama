# M001: User Management & Auth

**Vision:** Implement a secure, JWT-based authentication system with RBAC for both customers and admins, supporting Alanya-focused rental operations.

## Success Criteria
- [ ] Users can register and login successfully
- [ ] JWT tokens expire after 15 minutes
- [ ] Refresh tokens valid for 7 days
- [ ] Account locks after 5 failed login attempts
- [ ] Password reset email sent successfully

## Slices

- [x] **S01: Auth Domain & Persistence Foundation** `risk:low` `depends:[]`
  > After this: principal entities and migration are ready
- [x] **S02: JWT & Session Infrastructure** `risk:medium` `depends:[S01]`
  > After this: token generation and refresh logic implemented
- [x] **S03: Customer Auth API** `risk:medium` `depends:[S02]`
  > After this: register/login/refresh/logout endpoints for customers
- [x] **S04: Admin Auth, RBAC & Password Reset Backend** `risk:high` `depends:[S03]`
  > After this: admin/superadmin auth lifecycle, RBAC enforcement closure, and password-reset request/confirm email dispatch are implemented and verified
- [ ] **S05: Auth Frontend Integration** `risk:high` `depends:[S04]`
  > After this: customer/admin login flows, forgot/reset password UX, refresh/logout handling, and route-level RBAC guards work in Next.js

## Boundary Map (Remaining)

### S04 — Backend auth closure
- **Owns:** admin/superadmin auth endpoints, role-policy matrix verification, password-reset token issuance/validation, password-reset email dispatch contract, and backend tests for lockout/refresh/replay/reset flows.
- **Inputs:** S03 session lineage conventions (`revoked_at_utc`, `replaced_by_session_id`), shared JWT claim constants, existing unauthorized response contract.
- **Outputs to S05:** stable HTTP contracts for admin auth + password reset, cookie/session expectations, and RBAC claim behavior consumable by frontend middleware/guards.

### S05 — Frontend auth closure
- **Owns:** Next.js login/refresh/logout UX for customer/admin, forgot/reset password pages, token/cookie handling integration, and RBAC-aware route protection.
- **Inputs:** S04 backend contracts and RBAC behavior.
- **Outputs:** user-observable auth completion through UI and integration-level proof of primary auth loop continuity.

## Proof Strategy (Remaining)
- **S04:** controller/service/integration tests proving AUTH-06/07/08/09 behavior, including non-enumerating failures, lockout policy, reset token lifecycle, and email-send dispatch execution path.
- **S05:** frontend integration tests (and/or E2E) proving successful login, refresh continuity, logout cleanup, forgot/reset completion, and role-based route gating.

## Requirement Coverage (Active Auth)
- **AUTH-06 (password reset via email link):** S04 (backend reset lifecycle + email dispatch) + S05 (user flow integration)
- **AUTH-07 (admin login):** S04 (API + session lifecycle) + S05 (UI flow)
- **AUTH-08 (superadmin manages admin users):** S04 (RBAC + management endpoints/policies), with S05 consuming exposed admin interfaces as available
- **AUTH-09 (RBAC Guest/Customer/Admin/SuperAdmin):** S04 (policy closure + backend authorization matrix) + S05 (route/UI enforcement)
