---
id: S05
parent: M001
milestone: M001
provides:
  - Next.js auth integration closure with customer/admin login UX, forgot/reset password UX, BFF auth handlers, refresh/logout continuity, and proxy-level RBAC routing
requires:
  - slice: S04
    provides: stable admin/customer auth + password-reset backend contracts, refresh-cookie semantics, and RBAC role/policy behavior
affects:
  - M001 closure readiness
key_files:
  - frontend/proxy.ts
  - frontend/app/api/auth/login/route.ts
  - frontend/app/api/auth/refresh/route.ts
  - frontend/app/api/auth/logout/route.ts
  - frontend/app/api/auth/password-reset/request/route.ts
  - frontend/app/api/auth/password-reset/confirm/route.ts
  - frontend/components/auth/login-form.tsx
  - frontend/components/auth/forgot-password-form.tsx
  - frontend/components/auth/reset-password-form.tsx
  - frontend/components/layout/sidebar/nav-user.tsx
  - frontend/proxy.test.ts
key_decisions:
  - D021: auth frontend uses Next.js BFF route handlers + proxy guard with HttpOnly access cookie and backend refresh-cookie pass-through
patterns_established:
  - Principal-specific login flows (Customer vs Admin) share one reusable form component but use explicit `PrincipalScope`
  - Proxy is the single route-policy and refresh-continuity decision point for dashboard paths
  - Auth contract translation lives only in `/api/auth/*` handlers, not in page components
observability_surfaces:
  - `frontend/proxy.ts` route decisions + refresh branch
  - `frontend/app/api/auth/me/route.ts` principal diagnostics
  - `frontend/proxy.test.ts` RBAC/refresh assertions
  - `pnpm test`, `pnpm lint`, `pnpm build` in `frontend/`
drill_down_paths:
  - .gsd/milestones/M001/slices/S05/S05-PLAN.md
  - .gsd/milestones/M001/slices/S05/S05-RESEARCH.md
duration: 3h20m
verification_result: passed
completed_at: 2026-03-15
---

# S05: Auth Frontend Integration

**Shipped complete frontend auth integration: admin/customer login, forgot/reset password, refresh/logout continuity, and route-level RBAC guards now run in Next.js.**

## What Happened

S05 replaced template-only auth pages with working auth flows and introduced a BFF + proxy architecture aligned to backend contracts:

- Added **BFF auth handlers** under `frontend/app/api/auth/*` for login/register/refresh/logout/me/password-reset.
  - These handlers call backend endpoints server-side, normalize payloads, pass through `Set-Cookie`, and manage a short-lived HttpOnly access cookie.
- Rebuilt guest auth UX:
  - `/dashboard/login/v1` → customer login
  - `/dashboard/login/v2` → admin/superadmin login
  - `/dashboard/register/v1` → customer register (v2 redirects to v1)
  - `/dashboard/forgot-password` + new `/dashboard/reset-password`
- Replaced static `proxy.ts` redirect with **role-aware guard logic**:
  - Guest redirected to login for protected dashboard routes
  - Customer constrained to customer-only route surface
  - Admin blocked from superadmin-only route prefixes
  - Expired/missing access token triggers backend refresh attempt using refresh cookie
- Connected sidebar logout (`nav-user`) to `/api/auth/logout` and cleanup/redirect behavior.
- Added integration tests for proxy guard + login form submit contract.

## Verification

Executed all S05 slice verification checks defined in the S05 plan:

- `cd frontend && pnpm test` ✅ (3 files, 22 tests passed)
- `cd frontend && pnpm lint` ✅
- `cd frontend && pnpm build` ✅
- Browser runtime assertions on local app (`http://localhost:3000`) ✅
  - `/dashboard/default` redirects to `/dashboard/login/v2`
  - customer/admin login pages render
  - forgot/reset password pages render and are reachable

Observability/diagnostic surfaces were confirmed through:
- `frontend/proxy.test.ts` (RBAC + refresh branches)
- `frontend/app/api/auth/me/route.ts` (principal introspection)

## Requirements Advanced

- AUTH-06 — frontend forgot/reset password flow now operationalizes backend request/confirm contracts with principal scope.
- AUTH-07 — admin login is now user-reachable in UI and integrated with real backend session issuance.
- AUTH-09 — route-level RBAC enforcement is now active in Next.js proxy.

## Requirements Validated

- AUTH-06 — validated by working forgot/reset pages + BFF handlers wired to `/api/v1/auth/password-reset/*` and runtime/browser checks.
- AUTH-07 — validated by admin login UI + BFF `/api/auth/login` integration path and test/build/browser verification.
- AUTH-09 — validated by proxy guard logic + `proxy.test.ts` coverage for guest/customer/admin/superadmin routing outcomes.
- AUTH-02, AUTH-03, AUTH-04, AUTH-05, AUTH-10 (frontend continuity) — validated through integrated login/refresh/logout UX behavior, while backend lifecycle/lockout guarantees remain proven by S03/S04 tests.

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

none

## Known Limitations

- Password-reset email delivery is still backed by S04 logging stub dispatcher; end-to-end mailbox delivery is deferred to notification slices.
- Customer-facing authenticated area is currently a minimal `/dashboard/customer-portal` proof route, not final product UX.

## Follow-ups

- Expand customer authenticated area beyond proof route and align with future public-site flows.
- Add full E2E automation that includes backend runtime login + refresh + logout roundtrip with seeded users.

## Files Created/Modified

- `frontend/proxy.ts` — full RBAC/refresh route guard replacement.
- `frontend/app/api/auth/*` — BFF handlers for login/register/refresh/logout/me/password-reset.
- `frontend/components/auth/*` — reusable login/register/forgot/reset/customer portal components.
- `frontend/app/(admin)/dashboard/(guest)/login/v1/page.tsx` — customer login integration.
- `frontend/app/(admin)/dashboard/(guest)/login/v2/page.tsx` — admin login integration.
- `frontend/app/(admin)/dashboard/(guest)/forgot-password/page.tsx` — real request flow integration.
- `frontend/app/(admin)/dashboard/(guest)/reset-password/page.tsx` — new reset confirm page.
- `frontend/components/layout/sidebar/nav-user.tsx` — real logout wiring.
- `frontend/proxy.test.ts` — guard + refresh integration tests.
- `frontend/components/auth/login-form.test.tsx` — login submit contract test.

## Forward Intelligence

### What the next slice should know
- Keep all auth transport through `/api/auth/*`; avoid direct browser calls to backend auth endpoints.
- `proxy.ts` is now a critical security surface; route additions should be reviewed against role policy lists.

### What's fragile
- SuperAdmin-only prefixes are currently path-list based (`/dashboard/pages/users`); adding sensitive routes requires explicit list updates.

### Authoritative diagnostics
- `frontend/proxy.test.ts` — quickest high-signal check for role routing regressions.
- `pnpm build` — catches auth handler typing/contract drift early.

### What assumptions changed
- Original assumption: frontend could remain template-local and stateless. — Actual behavior: auth needed BFF handlers + cookie-aware proxy to satisfy backend contracts and RBAC continuity.
