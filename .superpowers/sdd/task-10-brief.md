### Task 10: Verification, Security Scan, and Handoff

**Files:**
- Modify only if verification finds concrete failures in files touched by prior tasks.

**Interfaces:**
- Consumes: all tasks above.
- Produces: verified implementation ready for review.

- [ ] **Step 1: Run backend tests**

Run:

```powershell
dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter "PublicSiteSettings|AdminPublicContent" --no-restore
```

Expected: PASS.

- [ ] **Step 2: Run frontend tests**

Run:

```powershell
corepack pnpm -C frontend test
```

Expected: PASS.

- [ ] **Step 3: Run frontend build**

Run:

```powershell
corepack pnpm -C frontend build
```

Expected: PASS.

- [ ] **Step 4: Run TypeScript check if build does not cover it**

Run:

```powershell
corepack pnpm -C frontend exec tsc --noEmit
```

Expected: PASS.

- [ ] **Step 5: Run Aikido scan**

Run `aikido_full_scan` on each generated, added, and modified first-party code file. Include full file content. Fix any reported issues and rerun until the scan reports zero issues for the modified scope.

- [ ] **Step 6: Update execution docs**

Append a short dated entry to `docs/10_Execution_Tracking.md`:

```markdown
**27 Jun 2026 Admin Public Content Management:** Admin content management moved to `/dashboard/settings/public-content` with draft/publish page workflow, sanitized rich text rendering, and separated contact editing. Verification: `dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter "PublicSiteSettings|AdminPublicContent" --no-restore` PASS, `corepack pnpm -C frontend test` PASS, `corepack pnpm -C frontend build` PASS, `corepack pnpm -C frontend exec tsc --noEmit` PASS, Aikido modified-file scan PASS with 0 issues.
```

Only append this entry after every listed command and scan has the matching PASS result. If any verification fails, fix the failure first and record the final passing command outcomes instead of this sentence.

- [ ] **Step 7: Final commit**

```powershell
git add docs/10_Execution_Tracking.md
git commit -m "docs(admin): record public content management verification"
```

## Self-Review

- Spec coverage: The plan covers separate admin route, page/contact scope, real draft/publish behavior, public draft isolation, rich text editor constraints, sanitizer policy, tests, build, and Aikido scan.
- Placeholder scan: The plan defines concrete interfaces, commands, and expected results without deferred fill-in markers.
- Type consistency: Backend DTO names match service signatures; frontend API types match client functions; `bodyFormat` is consistently `"plain" | "html"`.

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-27-admin-public-content-management.md`. Two execution options:

1. **Subagent-Driven (recommended)** - Dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints.
