# S05: Auth Frontend Integration — Research

**Date:** 2026-03-15

## Summary

`REQUIREMENTS.md` currently has **no Active auth requirements**, so S05 is a closure/integration slice rather than new requirement discovery. Practically, this slice supports delivery proof for already-validated backend requirements: **AUTH-06/07/09** directly, and user-visible continuity for **AUTH-02/03/04/05/10** (login, token expiry/refresh loop, logout revocation, lockout UX handling).

The biggest finding: frontend auth is still largely a dashboard template state. Login/register/forgot pages exist, but login/register are static forms (no backend calls), forgot-password is simulated with timeout/toast, there is no reset-password page, no auth store, no token lifecycle integration, and no RBAC route guard. Current `frontend/proxy.ts` only redirects `/` and `/dashboard` to `/dashboard/default`; it does not enforce auth.

Backend contracts are stable and stricter than a typical template frontend expects: separate customer/admin auth endpoints, principal-scoped password reset, refresh token in HttpOnly cookie, and non-enumerating failures. Also important: API currently has no configured CORS policy, so browser-direct frontend→API calls from different local origins are constrained. This strongly pushes S05 toward a **Next.js BFF-style integration** (route handlers + proxy guard), not direct client-side API coupling.

## Requirement Targeting (S05)

- **Active requirements owned:** none (per preloaded `REQUIREMENTS.md` Active bucket)
- **Validated requirements this slice must operationalize in UI/integration proof:**
  - AUTH-06 (forgot/reset UX with non-enumerating request semantics)
  - AUTH-07 (admin login flow)
  - AUTH-09 (route/UI RBAC enforcement)
  - plus continuity checks for AUTH-02/03/04/05/10 in frontend behavior

## Recommendation

Use a **Next.js 16 App Router + Proxy + BFF** approach:

1. Add auth-focused route handlers under `frontend/app/api/...` that call backend auth endpoints server-side.
2. Let those handlers normalize backend responses and own frontend-domain cookie/session state needed by `proxy.ts` route gating.
3. Upgrade `frontend/proxy.ts` from static redirect to policy-aware auth guard (guest vs authenticated vs role-restricted routes).
4. Integrate login/forgot/reset UI against real handlers (admin and customer scopes), preserving backend non-enumeration contract.
5. Add integration tests (Vitest + RTL minimum for forms/guards) and E2E proof path for login → protected route → refresh continuity → logout cleanup.

Why: this aligns with Next.js 16 proxy guidance, avoids CORS fragility, keeps refresh cookie handling controllable, and gives S05 a clean seam for RBAC enforcement without leaking backend token mechanics into every component.

## Don’t Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|-------------------|------------|
| Auth forms + validation UX | Existing shadcn form stack (`react-hook-form` + `zod` + `Form` components) already used in forgot page | Keeps visual/validation consistency and reduces custom form state bugs |
| Refresh token storage semantics | Backend `RefreshTokenCookieService` + `RefreshTokenCookieSettings` contracts | Preserves secure cookie assumptions and avoids divergent cookie names/options |
| RBAC role vocabulary | Backend `AuthRoleNames` canonical values (`Customer`, `Admin`, `SuperAdmin`) | Prevents role-string drift between UI checks and API policies |
| Unauthorized contract parsing | Backend `ApiResponse<object>` with generic `Yetkisiz erişim` | Prevents account/session-state leakage via frontend message branching |

## Existing Code and Patterns

- `frontend/proxy.ts` — currently only redirects `/` and `/dashboard` to `/dashboard/default`; this is the central place to evolve into auth/rbac route protection.
- `frontend/app/(admin)/dashboard/(guest)/login/v1/page.tsx` and `.../login/v2/page.tsx` — UI-only forms, no submit handlers wired to backend.
- `frontend/app/(admin)/dashboard/(guest)/forgot-password/page.tsx` — best current auth-form pattern (`react-hook-form` + zod + sonner), but still mock submission.
- `frontend/app/(admin)/dashboard/(auth)/layout.tsx` — all authenticated dashboard pages currently render without session/role checks.
- `frontend/components/layout/sidebar/nav-user.tsx` — logout action is present visually but not connected to auth endpoint/session cleanup.
- `backend/src/RentACar.API/Controllers/AdminAuthController.cs` — admin login/refresh/logout contract and lockout behavior.
- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs` — customer login/register/refresh/logout + session rotation semantics.
- `backend/src/RentACar.API/Controllers/PasswordResetController.cs` — reset request/confirm requires `PrincipalScope` (`Admin`/`Customer`), request is intentionally non-enumerating.
- `backend/src/RentACar.API/Options/RefreshTokenCookieSettings.cs` + `appsettings.Development.json` — cookie name is env-sensitive (`rac_refresh` in dev, `__Host-rac_refresh` default).
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — JWT auth + policy map + unauthorized payload; no explicit CORS policy configured.
- `backend/tests/RentACar.Tests/Unit/Controllers/RbacPolicyMatrixTests.cs` — authoritative backend policy matrix for guest/customer/admin/superadmin endpoint boundaries.

## Constraints

- No Active requirement bucket to close; S05 must prove frontend integration against already-validated backend auth contracts.
- Backend auth endpoints are split by principal:
  - `api/customer/v1/auth/*`
  - `api/admin/v1/auth/*`
  - `api/v1/auth/password-reset/*` with required `PrincipalScope`
- Password reset request is non-enumerating by design; UI must not reveal account existence.
- Unauthorized responses are generic (`ApiResponse<object>.Fail("Yetkisiz erişim")`) across many failure branches.
- Refresh/logout semantics depend on server-side session lineage (`revoked_at_utc`, `replaced_by_session_id`), not just client token overwrite.
- API project has no app-level CORS configuration surfaced in startup pipeline.
- Frontend has no existing API layer/auth store/route guard abstraction.
- No frontend auth tests exist yet beyond utility tests.

## Common Pitfalls

- **Assuming one login fits both principal types** — backend contracts are principal-specific; split flows or explicit scope handling is required.
- **Leaking account existence in forgot-password UI** — backend intentionally returns success even when user is missing/inactive.
- **Trying browser-direct cross-origin auth calls first** — likely to hit cookie/CORS friction; BFF route handlers are safer.
- **Treating refresh as stateless** — backend rotates sessions and revokes predecessor; stale refresh attempts should surface as generic unauthorized.
- **Client-only RBAC hiding** — UI hiding alone is insufficient; route-level guard + backend policy alignment required.

## Open Risks

- Customer-facing route surface is still mostly dashboard-template oriented; customer auth UX destination needs explicit product decision.
- Cookie-domain strategy is undecided for frontend guardability (especially if backend and frontend remain different origins in dev/prod).
- No current reset-password page exists; token capture/validation UX must be newly introduced.
- Password reset email dispatch remains a logging-backed stub backend-side; full end-to-end email-link validation is constrained until notification slice completion.

## Skill Discovery

_No GSD Skill Preferences block was present in system context._

| Technology | Skill | Status |
|------------|-------|--------|
| Next.js App Router auth/proxy | `vercel-react-best-practices` (installed) | installed |
| Frontend UI verification / E2E | `webapp-testing`, `e2e-testing-patterns` (installed) | installed |
| Zustand state layer (if adopted) | `jezweb/claude-skills@zustand-state-management` | available (install: `npx skills add jezweb/claude-skills@zustand-state-management`) |
| React Hook Form + Zod hardening | `jezweb/claude-skills@react-hook-form-zod` | available (install: `npx skills add jezweb/claude-skills@react-hook-form-zod`) |
| JWT security review | `mindrally/skills@jwt-security` | available (install: `npx skills add mindrally/skills@jwt-security`) |

## Sources

- Frontend route/auth baseline and gaps (source: `frontend/app/(admin)/dashboard/(guest)/login/v1/page.tsx`)
- Frontend route/auth baseline and gaps (source: `frontend/app/(admin)/dashboard/(guest)/login/v2/page.tsx`)
- Existing forgot-password pattern (source: `frontend/app/(admin)/dashboard/(guest)/forgot-password/page.tsx`)
- Current route redirect behavior (source: `frontend/proxy.ts`)
- Auth area currently ungated (source: `frontend/app/(admin)/dashboard/(auth)/layout.tsx`)
- Static nav/logout without auth integration (source: `frontend/components/layout/sidebar/nav-user.tsx`, `frontend/components/layout/sidebar/nav-main.tsx`)
- Admin auth lifecycle contract (source: `backend/src/RentACar.API/Controllers/AdminAuthController.cs`)
- Customer auth lifecycle contract (source: `backend/src/RentACar.API/Controllers/CustomerAuthController.cs`)
- Password reset principal-scoped contract (source: `backend/src/RentACar.API/Controllers/PasswordResetController.cs`)
- Auth response envelope contract (source: `backend/src/RentACar.API/Controllers/BaseApiController.cs`, `backend/src/RentACar.API/Contracts/ApiResponse.cs`)
- Cookie naming and security settings (source: `backend/src/RentACar.API/Options/RefreshTokenCookieSettings.cs`, `backend/src/RentACar.API/appsettings.Development.json`)
- Policy and unauthorized behavior (source: `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs`)
- Policy matrix truth set (source: `backend/tests/RentACar.Tests/Unit/Controllers/RbacPolicyMatrixTests.cs`)
- Next.js 16 proxy/cookie route-protection guidance (source: Context7 `/vercel/next.js`, query: `proxy.ts replacing middleware auth route protection cookies headers next.js 16 app router`)
- Next.js cookie handling in server functions/route handlers (source: Context7 `/vercel/next.js`, query: `app router route handlers forward Set-Cookie from backend fetch credentials include cookies Next.js 16`)
- External skill discovery results (source: `npx skills find "zustand"`, `npx skills find "react hook form"`, `npx skills find "jwt authentication"`)
