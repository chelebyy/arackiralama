---
phase: 6
slug: user-management-auth
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-14
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (.NET) + Vitest (frontend) |
| **Config file** | `backend/tests/RentACar.Tests/RentACar.Tests.csproj`, `frontend/vitest.config.ts` |
| **Quick run command** | `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore` |
| **Full suite command** | `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore && pnpm -C frontend test` |
| **Estimated runtime** | ~90 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore`
- **After every plan wave:** Run `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore && pnpm -C frontend test`
- **Before `$gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| P6-01-T1 | 01 | 1 | AUTH-01, AUTH-06, AUTH-08 | unit | `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore --filter AuthDomain` | ❌ W0 | ⬜ pending |
| P6-01-T4 | 01 | 1 | AUTH-01, AUTH-07 | integration | `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore --filter Migration` | ❌ W0 | ⬜ pending |
| P6-02-T* | 02 | 1 | AUTH-03, AUTH-04, AUTH-07 | unit/integration | `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore --filter Token` | ❌ W0 | ⬜ pending |
| P6-03-T* | 03 | 2 | AUTH-01, AUTH-02, AUTH-04, AUTH-05, AUTH-07, AUTH-08 | integration | `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore --filter CustomerAuth` | ❌ W0 | ⬜ pending |
| P6-04-T* | 04 | 2 | AUTH-02, AUTH-06, AUTH-07, AUTH-09, AUTH-10 | integration | `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore --filter AdminAuth` | ❌ W0 | ⬜ pending |
| P6-05-T2 | 05 | 3 | AUTH-01, AUTH-02, AUTH-05, AUTH-07 | frontend | `pnpm -C frontend test -- --runInBand auth` | ❌ W0 | ⬜ pending |
| P6-05-T3 | 05 | 3 | AUTH-01, AUTH-02, AUTH-03, AUTH-04, AUTH-05, AUTH-06, AUTH-07, AUTH-08, AUTH-09, AUTH-10 | full | `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore && pnpm -C frontend test` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `backend/tests/RentACar.Tests/Auth/` — backend auth-focused unit/integration suites for customer/admin/session/reset flows
- [ ] `frontend/tests/auth/` — frontend/Vitest coverage for auth page wiring, redirects, and session bootstrap
- [ ] Shared test data fixtures for normalized email, guest-to-registered customer claiming, revoked sessions, and lockout scenarios

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Reset email copy is concise and security-first | AUTH-05 | Email wording and UX tone are content-sensitive | Trigger forgot-password flow and review generated template/copy for generic, non-enumerating wording. |
| Admin deactivation immediately removes active dashboard access | AUTH-10 | Requires end-to-end active session observation across browser state | Sign in as admin in browser A, deactivate same admin from SuperAdmin browser B, confirm browser A loses access on next protected request/refresh. |
| Guest reservation flow still works without forced login | AUTH-01, AUTH-07 | Cross-phase compatibility with existing booking UX | Execute existing guest booking path and confirm customer auth additions do not block reservation creation. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
