# S05: Auth Frontend Integration

**Goal:** close frontend auth loop for customer/admin login, forgot/reset UX, refresh/logout continuity, and RBAC route protection in Next.js.

**Demo:** guest user is redirected to login for protected dashboard routes; admin and customer can log in via their own flows; forgot/reset forms call real backend contracts through Next.js BFF handlers; refresh keeps session continuity; logout clears local auth state and backend refresh cookie lineage.

## Decomposition Rationale

S05 is implemented as one integrated frontend closure because auth behavior crosses four coupled layers:

1. **BFF API handlers** (`/api/auth/*`) to bridge Next.js and backend auth endpoints without browser CORS/cookie fragility.
2. **Proxy guard** (`frontend/proxy.ts`) for route-level guest/customer/admin/superadmin gating and refresh continuity.
3. **Guest auth UX** (login/register/forgot/reset pages) to operationalize backend contracts in user flows.
4. **Session UX surfaces** (logout + customer portal + profile fetch) to prove post-login continuity and cleanup.

## Must-Haves

- Admin login flow wired to `api/admin/v1/auth/login` through frontend BFF.
- Customer login flow wired to `api/customer/v1/auth/login` through frontend BFF.
- Forgot-password and reset-password UIs wired to password-reset request/confirm contracts with `PrincipalScope`.
- Refresh handled via frontend BFF + proxy so expired access cookie can be renewed from refresh cookie.
- Logout calls backend logout and clears frontend access/refresh cookie state.
- Route-level RBAC in proxy for Guest, Customer, Admin, SuperAdmin (including superadmin-only route guard).

## Requirement Coverage

- **AUTH-06:** Forgot/reset password frontend integration (request + confirm).
- **AUTH-07:** Admin login frontend flow integration.
- **AUTH-09:** Route/UI RBAC enforcement in Next.js proxy and protected route behavior.
- **Continuity proof for AUTH-02/03/04/05/10:** customer/admin login, token expiry/refresh, logout cleanup, lockout-compatible generic error handling.

## Proof Level

- This slice proves: integration
- Real runtime required: yes (frontend build/runtime + local browser checks)
- Human/UAT required: no (artifact-driven + explicit UAT script delivered)

## Verification

- `cd frontend && pnpm test`
- `cd frontend && pnpm lint`
- `cd frontend && pnpm build`
- Browser assertions on local app:
  - `/dashboard/default` redirects to `/dashboard/login/v2`
  - Login/forgot/reset pages render and are reachable

## Observability / Diagnostics

- `frontend/proxy.ts` is the single route-gating/refresh decision surface.
- `frontend/app/api/auth/*` handlers are the contract seam for backend auth lifecycle.
- `frontend/app/api/auth/me` exposes current principal/role for UI diagnostics.
- `frontend/proxy.test.ts` captures guest/customer/admin/superadmin route outcomes and refresh cookie behavior.
- `frontend/components/auth/login-form.test.tsx` captures submit contract + redirect behavior.

## Tasks

- [x] Implement Next.js BFF auth handlers (`/api/auth/login|register|refresh|logout|me|password-reset/*`).
- [x] Replace static auth pages with working customer/admin login and forgot/reset forms.
- [x] Add customer-portal and forbidden guest routes to demonstrate RBAC outcomes.
- [x] Replace static `proxy.ts` redirect with role-aware guard + refresh continuity.
- [x] Wire sidebar logout to real `/api/auth/logout` cleanup flow.
- [x] Add frontend integration tests for proxy guard and login submission path.

## Files Likely Touched

- `frontend/proxy.ts`
- `frontend/app/api/auth/**`
- `frontend/app/(admin)/dashboard/(guest)/login/**`
- `frontend/app/(admin)/dashboard/(guest)/register/**`
- `frontend/app/(admin)/dashboard/(guest)/forgot-password/page.tsx`
- `frontend/app/(admin)/dashboard/(guest)/reset-password/page.tsx`
- `frontend/app/(admin)/dashboard/(guest)/customer-portal/page.tsx`
- `frontend/app/(admin)/dashboard/(guest)/forbidden/page.tsx`
- `frontend/components/auth/**`
- `frontend/components/layout/sidebar/nav-user.tsx`
- `frontend/lib/auth/**`
- `frontend/proxy.test.ts`
- `frontend/components/auth/login-form.test.tsx`
