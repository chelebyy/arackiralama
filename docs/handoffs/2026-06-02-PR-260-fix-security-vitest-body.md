# fix(security): bump vitest to 4.1.x to address CVE-2026-47429

Closes the two open Dependabot critical alerts on `main`:
- **#37** — `frontend/package.json`: vitest < 4.1.0
- **#38** — `frontend/pnpm-lock.yaml`: vitest < 4.1.0

Both point to **[GHSA-5xrq-8626-4rwp](https://github.com/advisories/GHSA-5xrq-8626-4rwp) / CVE-2026-47429**:
> When Vitest UI server is listening, arbitrary file can be read and executed.

## Change

Bumped vitest and its coverage plugin to the patched 4.1.x line:

| Package | Before | After (constraint) | Resolved |
|---------|--------|--------------------|----------|
| `vitest` | `^3.2.4` | `^4.1.0` | `4.1.8` |
| `@vitest/coverage-v8` | `^3.2.4` | `^4.1.0` | `4.1.8` |

Two files touched: `frontend/package.json`, `frontend/pnpm-lock.yaml`.

## Prerequisites already met (no changes required)

Vitest 4.x requires:
- **Vite >= 6.4.0** — we already have `7.3.2` (peer dep satisfied).
- **Node >= 22.12.0** — local Node is `v24.13.0`.

## Vitest 4 migration considerations

Vitest 4 removes two deprecated config options:
- `poolMatchGlobs` (use `projects` instead)
- `environmentMatchGlobs` (use `projects` instead)

Our `vitest.config.ts` uses **neither** — no config change required.

## Verification (run against this branch)

| Check | Result |
|-------|--------|
| `pnpm audit` | **0 critical**, 0 high — 1 transitive moderate (`brace-expansion` via `eslint-config-next > eslint-plugin-import > ... > minimatch > brace-expansion`) — out of scope for this PR (parent-package fix path) |
| `pnpm test` | **190/190 PASS** (46 test files, 21.38s) |
| `pnpm build` | **0 error** (Next.js 16 App Router, all routes compiled) |
| `pnpm lint` | **0 error** (1 pre-existing warning in `SearchForm.test.tsx:45` — unused `eslint-disable` directive, unrelated to this change) |

## Out of scope (deliberately deferred)

- **Transitive `brace-expansion` moderate** — comes from the `eslint-config-next > eslint-plugin-import` chain. Fix path is either a `pnpm.overrides` patch or waiting for the parent package to update. Should be addressed in a separate PR with a clear override rationale.
- **9 DEFERRED Phase 10 launch gates** — all Dokploy-dependent, out of scope for security fix.

## Merge plan

- **Base:** `main` (`bbf0660`)
- **Branch:** `fix/security-vitest-2026-06-02` (1 commit, `220d602`)
- **Strategy:** fast-forward / squash-merge. No migration, no schema change, no public surface change.
- **CI gates expected to pass:** backend build/test, frontend lint/test/build, docker build, GHCR push (main only).
- **Post-merge:** GitHub will auto-close Dependabot alerts #37 and #38 once `main` HEAD detects the patched range.

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
