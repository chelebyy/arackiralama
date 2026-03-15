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
- [ ] **S04: Admin Auth & RBAC** `risk:medium` `depends:[S03]`
  > After this: admin authentication and role-based policies
- [ ] **S05: Auth Frontend Integration** `risk:high` `depends:[S04]`
  > After this: users can login through the Next.js UI
