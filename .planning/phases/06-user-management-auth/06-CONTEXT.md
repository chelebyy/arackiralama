# Phase 6: User Management & Auth - Context

**Gathered:** 2026-03-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Implement customer and admin authentication with JWT + RBAC, including registration, login, refresh/logout behavior, password reset, admin user control, and account lockout. This phase does not add social login, 2FA, or other new auth capabilities outside the roadmap.

</domain>

<decisions>
## Implementation Decisions

### Session Policy
- Customers can stay signed in on multiple devices at the same time.
- Standard logout invalidates only the current device/session.
- Refresh tokens rotate on every refresh request.
- Sessions should silently refresh while the user stays active, up to the refresh token lifetime.

### Recovery Flow
- Password reset links remain valid for 30 minutes.
- Accounts locked after failed attempts unlock automatically after a time-based cooldown.
- Completing a password reset logs the user out on all devices.
- Password reset email copy should be concise and security-first.

### Admin Boundaries
- Only SuperAdmin can create new admin users.
- Only SuperAdmin can change admin roles.
- Deactivating an admin account should terminate active sessions immediately.
- Audit logging must treat auth events, role changes, and admin-user changes as critical.

### Claude's Discretion
- Exact cooldown duration for lockout within a reasonable operational range.
- Refresh token storage model and revocation data shape.
- Exact reset email wording and UI microcopy.
- Exact audit event schema, provided the required operations are covered.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `backend/src/RentACar.API/Services/JwtTokenService.cs`: Existing JWT issuing logic for admin authentication can be extended rather than replaced.
- `backend/src/RentACar.Infrastructure/Security/BcryptPasswordHasher.cs`: Existing password hashing service is ready for customer auth flows.
- `backend/src/RentACar.API/Controllers/AdminAuthController.cs`: Existing admin auth controller provides a starting point for shared patterns and response shapes.
- `frontend/app/(admin)/dashboard/(guest)/login/*`, `register/*`, `forgot-password/page.tsx`: Existing auth page templates can be adapted for the product auth flow instead of building screens from scratch.

### Established Patterns
- Backend already uses policy-based authorization with `AdminOnly` and `SuperAdminOnly`.
- API service layer pattern is already in place (`*Service`, controller + contract separation).
- Frontend already has a shadcn/ui component library and guest/auth route split.

### Integration Points
- New customer auth endpoints should align with the existing API/controller/service conventions in `backend/src/RentACar.API`.
- Admin auth expansion should integrate with current JWT/policy wiring in `backend/src/RentACar.API/Configuration`.
- Frontend auth UX should plug into existing guest auth pages and admin dashboard route structure.

</code_context>

<specifics>
## Specific Ideas

- Admin security should stay stricter than customer convenience.
- The phase should preserve a smooth reservation experience; auth must not add unnecessary friction for customers.
- Logout-all-on-password-reset is important enough to treat as the default security behavior.

</specifics>

<deferred>
## Deferred Ideas

- Google/Facebook social login
- Two-factor authentication via SMS or TOTP

</deferred>

---

*Phase: 06-user-management-auth*
*Context gathered: 2026-03-14*
