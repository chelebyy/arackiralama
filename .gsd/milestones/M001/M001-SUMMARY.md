---
id: M001
provides:
  - End-to-end auth closure for customer/admin principals with JWT+refresh session lifecycle, lockout enforcement, password reset lifecycle, RBAC policy closure, and Next.js BFF/proxy integration.
key_decisions:
  - D008/D009/D010 established shared JWT claims (`sid`,`ver`), 15-minute access tokens, hash-at-rest refresh tokens, and DB-backed session validation as authorization authority.
  - D018/D019 aligned admin lifecycle parity with customer semantics and formalized principal-aware, non-enumerating password reset with hash-at-rest token rows and dispatch abstraction.
  - D021 implemented frontend auth via `/api/auth/*` BFF handlers + `proxy.ts` RBAC guard with HttpOnly `rac_access` cookie and backend refresh-cookie pass-through.
patterns_established:
  - Session-authoritative auth: signed JWT is accepted only when backing session/token-version state remains valid in DB.
  - Refresh-token rotation lineage: replaced sessions are revoked-equivalent via `revoked_at_utc` + `replaced_by_session_id`.
  - Contract translation boundary: frontend pages never call backend auth directly; all transport flows through Next.js BFF handlers.
observability_surfaces:
  - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~JwtTokenServiceTests|FullyQualifiedName~AccessTokenSessionValidatorTests"`
  - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~CustomerAuthControllerTests|FullyQualifiedName~AdminAuthControllerTests|FullyQualifiedName~PasswordResetControllerTests"`
  - `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminUsersControllerTests|FullyQualifiedName~RbacPolicyMatrixTests|FullyQualifiedName~AuthConventionsTests"`
  - `cd frontend && pnpm test && pnpm lint && pnpm build`
  - `frontend/proxy.test.ts` role-routing/refresh assertions + browser runtime checks at `http://localhost:3000`
requirement_outcomes:
  - id: AUTH-01
    from_status: active
    to_status: validated
    proof: S03 `CustomerAuthController` register flow and `CustomerAuthControllerTests` registration coverage proved customer registration success paths.
  - id: AUTH-02
    from_status: active
    to_status: validated
    proof: S03 customer login endpoint/tests plus S05 login-form/BFF integration and runtime route checks validated login continuity.
  - id: AUTH-03
    from_status: active
    to_status: validated
    proof: S02 `JwtTokenServiceTests` verified 15-minute access-token lifetime and mandatory auth claim contract.
  - id: AUTH-04
    from_status: active
    to_status: validated
    proof: S02 refresh-token primitives (7-day expiry, hash-at-rest, constant-time verify) + S03/S04 refresh rotation/replay rejection tests.
  - id: AUTH-05
    from_status: active
    to_status: validated
    proof: S03/S04 logout revocation tests validated server-side session invalidation and cookie cleanup; S05 wired frontend logout through BFF.
  - id: AUTH-06
    from_status: active
    to_status: validated
    proof: S04 `PasswordResetControllerTests` validated request/confirm lifecycle (non-enumeration, single-use, expiry checks, token-version/session invalidation) and dispatch execution path; S05 added forgot/reset UX handlers.
  - id: AUTH-07
    from_status: active
    to_status: validated
    proof: S04 admin login/refresh/logout lifecycle tests validated normalized-email auth, lockout, rotation lineage, and revocation behavior; S05 exposed admin login UI.
  - id: AUTH-08
    from_status: active
    to_status: validated
    proof: S04 `AdminUsersControllerTests` validated SuperAdmin-only admin management endpoints (list/create/update-role/activate/deactivate/reset-initiation).
  - id: AUTH-09
    from_status: active
    to_status: validated
    proof: S04 `RbacPolicyMatrixTests` + `AuthConventionsTests` locked backend policy boundaries; S05 `proxy.test.ts` validated Guest/Customer/Admin/SuperAdmin route gating in Next.js.
  - id: AUTH-10
    from_status: active
    to_status: validated
    proof: S03/S04 login failure branch tests validated deterministic lockout (5 attempts, 15-minute lockout window) with persisted auth-state mutation.
duration: 16h59m
verification_result: passed
completed_at: 2026-03-15
---

# M001: User Management & Auth

**Delivered production-shaped auth closure across backend and frontend: JWT/session lifecycle, refresh rotation, lockout, password reset, admin/superadmin RBAC, and Next.js guard integration now operate as one coherent system.**

## What Happened

M001 progressed from persistence foundations (S01) to runtime session authority (S02), then closed customer auth (S03), admin/reset/RBAC backend behavior (S04), and finally Next.js integration (S05).

Together, these slices established a single end-to-end contract:

- principals (Customer/Admin/SuperAdmin) authenticate through normalized-email identity,
- short-lived access tokens are bound to DB session state (`sid`/`ver`),
- refresh flows rotate sessions with replay-resistant lineage,
- logout/reset invalidate server-side session authority,
- frontend consumes all auth behavior through BFF handlers and route-level proxy guards.

This milestone converted auth from partial backend primitives into a user-observable, role-aware, full lifecycle loop.

## Cross-Slice Verification

Success criteria verification from roadmap:

1. **Users can register and login successfully** ✅
   - S03: `CustomerAuthControllerTests` validated register/login success/failure branches.
   - S05: login pages + `/api/auth/login` BFF path validated by tests/build/browser checks.

2. **JWT tokens expire after 15 minutes** ✅
   - S02: `JwtTokenServiceTests` asserted 15-minute access token lifetime and contract claims.

3. **Refresh tokens valid for 7 days** ✅
   - S02: refresh token lifetime/verification primitives validated.
   - S03/S04: refresh endpoint lifecycle tests validated active-session + rotation semantics.

4. **Account locks after 5 failed login attempts** ✅
   - S03/S04: customer/admin login lockout branch tests validated 5-attempt threshold and 15-minute lockout state.

5. **Password reset email sent successfully** ✅ *(milestone-scope dispatch path)*
   - S04: request flow persists reset token hash and executes dispatcher contract path under test coverage.
   - S05: forgot/reset UI successfully integrates with backend reset request/confirm APIs.
   - Note: real mailbox-provider delivery is intentionally deferred; current dispatcher is logging-backed but contract execution is verified.

Definition of done verification:

- **All slices complete:** S01, S02, S03, S04, S05 are `[x]`.
- **All slice summaries exist:** verified for `S01-SUMMARY.md` through `S05-SUMMARY.md`.
- **Cross-slice integration works:** S04 backend contracts are consumed by S05 BFF/proxy integration; `pnpm test/lint/build` and runtime route checks passed.

## Requirement Changes

- AUTH-01: active → validated — S03 register endpoint + tests.
- AUTH-02: active → validated — S03 login endpoint + S05 UI/BFF integration verification.
- AUTH-03: active → validated — S02 JWT 15-minute lifetime tests.
- AUTH-04: active → validated — S02 refresh primitives + S03/S04 rotation/replay tests.
- AUTH-05: active → validated — S03/S04 logout revocation + S05 logout integration.
- AUTH-06: active → validated — S04 reset lifecycle tests + S05 forgot/reset UX integration.
- AUTH-07: active → validated — S04 admin auth lifecycle tests + S05 admin login UX.
- AUTH-08: active → validated — S04 SuperAdmin admin-management endpoint tests.
- AUTH-09: active → validated — S04 backend RBAC matrix + S05 proxy guard tests.
- AUTH-10: active → validated — S03/S04 lockout policy tests.

## Forward Intelligence

### What the next milestone should know
- Keep auth transport centralized through `frontend/app/api/auth/*`; bypassing BFF risks cookie/contract drift.
- RBAC additions must update both backend policy assignments and `frontend/proxy.ts` role path lists.

### What's fragile
- Password-reset delivery is contract-complete but transport-stubbed (logging dispatcher); notification milestone must replace it with real provider integration.
- SuperAdmin-only route protection currently relies on explicit path-prefix lists; new sensitive routes need deliberate registration.

### Authoritative diagnostics
- `backend/tests/RentACar.Tests/Unit/Controllers/PasswordResetControllerTests.cs` — best signal for reset token lifecycle + principal invalidation guarantees.
- `backend/tests/RentACar.Tests/Unit/Controllers/RbacPolicyMatrixTests.cs` and `frontend/proxy.test.ts` — fastest drift detection for backend/frontend RBAC boundaries.
- `backend/tests/RentACar.Tests/Unit/Services/AccessTokenSessionValidatorTests.cs` — canonical proof that rotated/revoked sessions are denied.

### What assumptions changed
- Assumption: JWT signature/lifetime alone could be sufficient. → Actual: DB session + token-version checks are mandatory for trust.
- Assumption: frontend could stay template-local for auth. → Actual: BFF route handlers + proxy guard were required for secure continuity.

## Files Created/Modified

- `.gsd/milestones/M001/M001-SUMMARY.md` — milestone closure record with success-criteria, DoD, and requirement-transition evidence.
- `.gsd/REQUIREMENTS.md` — status-bucket framing aligned to milestone completion evidence.
- `.gsd/PROJECT.md` — project state updated to reflect M001 completion and next active work.
- `.gsd/STATE.md` — GSD runtime state updated for post-M001 handoff.
