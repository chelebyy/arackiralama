<!-- lemma:start -->
## Lemma — Persistent Memory

<identity>
You have persistent memory via Lemma. Sessions start blank — only Lemma tool calls carry knowledge forward.
If you don't call memory_add, the insight is gone permanently.
</identity>

<mandatory_rules>
1. ALWAYS call memory_read before any task. Your memory may already have the answer.
2. ALWAYS call memory_add after: solving a bug, discovering a pattern, making a decision, understanding architecture.
3. NEVER say "I'll remember this" — you won't. Execute memory_add immediately.
4. NEVER re-explore codebase for something already in memory. Check memory first.
5. When memory contradicts observable reality, trust reality → call memory_update.
</mandatory_rules>

<workflow>
FOR EVERY TASK, follow this order:
1. RECALL: memory_read → guide_get (check what you know)
2. ACT: implement, search, analyze (only if memory didn't answer it)
3. PERSIST: memory_add or guide_practice (save before responding to user)
</workflow>

<maintenance>
When you notice these situations, act immediately:
- Outdated memory → memory_update (don't act on stale knowledge)
- Duplicate memories → memory_merge (scattered duplicates weaken retrieval)
- Irrelevant memory → memory_forget (clutter buries what matters)
- Related memories → memory_relate (connected knowledge is resilient)
- Useful memory after use → memory_feedback useful=true (ranks good memories higher)
</maintenance>

<knowledge_pipeline>
Static knowledge (memory_add) → Reusable skills (guide_distill, guide_practice).

Fragment types for memory_add: fact | pattern | lesson | warning | context.
When the same pattern appears in 3+ memories, distill it: guide_distill.
</knowledge_pipeline>

<session_management>
- Sessions start automatically when you make your first tool call.
- session_end: record outcome and lessons when your task is complete.
- Periodically: memory_stats + memory_audit to clean orphans and low-confidence noise.
</session_management>
<!-- lemma:end -->

# Repository Guidelines

**Generated:** 2026-04-23
**Stack:** .NET 10 + PostgreSQL + Redis | Next.js 16 + React 19 + TypeScript
**Pattern:** Clean Architecture (API/Core/Infrastructure/Worker)

## Project Structure & Module Organization
```
.
├── backend/
│   ├── src/RentACar.API/           # Controllers, middleware, auth, app services (116 files)
│   ├── src/RentACar.Core/          # Entities, interfaces, enums, constants (48 files)
│   ├── src/RentACar.Infrastructure/# EF Core, repos, external services (66 files)
│   ├── src/RentACar.Worker/        # Background job host (minimal/stubbed)
│   └── tests/RentACar.Tests/       # xUnit unit + integration tests
├── frontend/
│   ├── app/                        # Next.js App Router with route groups
│   ├── components/                 # UI components (shadcn/ui + custom)
│   ├── hooks/                      # Custom React hooks
│   ├── lib/                        # API clients, auth utilities
│   └── i18n/                       # next-intl config + message files
├── .github/workflows/              # CI: dotnet + pnpm + docker
├── docs/                           # Execution notes + planning docs
└── AGENTS.md                       # This file
```

## Where to Look
| Task | Location | Notes |
|------|----------|-------|
| Add API endpoint | `backend/src/RentACar.API/Controllers/` | 22 controllers, RESTful |
| Add domain entity | `backend/src/RentACar.Core/Entities/` | 18 entities, EF-mapped |
| Add EF migration | `backend/src/RentACar.Infrastructure/Data/Migrations/` | Npgsql provider |
| Add background job | `backend/src/RentACar.Worker/Worker.cs` | Polls every 30s |
| Add public page | `frontend/app/(public)/[locale]/` | i18n-aware |
| Add admin page | `frontend/app/(admin)/dashboard/(auth)/` | Requires auth |
| Add UI component | `frontend/components/ui/` | shadcn/ui + custom |
| Update translations | `frontend/i18n/messages/` | ar, de, en, ru, tr |

## Backend Conventions
- **Non-standard placement**: Application services live in `RentACar.API/Services/` (20 files) instead of a separate Application layer or Infrastructure.
- `Contracts/` folders hold request/response DTOs per domain (Fleet, Auth, Pricing, Reservations).
- `Specifications/` exists in both Core and Infrastructure for CQRS-style queries.
- Worker project is minimal; background processing logic is in `Worker.cs` polling pattern.
- `Program.cs` uses `WebApplication.CreateBuilder` with `AddApiApplicationServices()` extension method.

## Frontend Conventions
- Route groups: `(admin)` and `(public)` segment layouts without affecting URL.
- `(admin)/dashboard/(auth)/` requires authentication; `(admin)/dashboard/(guest)/` is for login.
- `(public)/[locale]/` handles i18n routing for all public-facing pages.
- `app/page.tsx` hardcodes `redirect("/tr")` — entry point for locale selection.
- `app/api/` contains Next.js Route Handlers for internal API proxying.
- **Design rule**: Public pages must NOT use shadcn/ui components or design language (corporate-minimal, light-only, desktop-first).
- Admin/dashboard CAN use shadcn/ui components.

## Commands
```bash
# Backend
dotnet restore backend/RentACar.sln --configfile backend/NuGet.Config
dotnet build backend/RentACar.sln --no-restore
dotnet test backend/RentACar.sln --no-build
dotnet run --project backend/src/RentACar.API

# Frontend
corepack pnpm -C frontend install
corepack pnpm -C frontend dev
corepack pnpm -C frontend build
corepack pnpm -C frontend test

# Docker (local dev)
cd backend && docker compose up --build
```

## CI Pipeline
- `.github/workflows/ci.yml`: backend build/test → frontend lint/test/build → docker build → docker push to GHCR (main only)
- `.github/workflows/dependabot-auto-merge.yml`: auto-merges patch/minor Dependabot PRs
- `.github/workflows/codeql.yml`: security scanning for C# and JS/TS

## Coding Style & Naming Conventions
- C#: 4-space indent, PascalCase types/methods, camelCase locals/parameters.
- TypeScript/React: 2-space indent, PascalCase components, camelCase variables/functions.
- Keep services explicit and small; avoid unnecessary abstractions.
- Run lint/format before PR (`corepack pnpm -C frontend lint`).

## Testing Guidelines
- Backend: xUnit; place tests under `backend/tests/RentACar.Tests/Unit/...`.
- Frontend: Vitest + Testing Library; test files use `*.test.ts` / `*.test.tsx`.
- Add or update tests for any behavior change, especially auth, payments, reservations, and notifications.
- Keep tests deterministic (no real external network dependencies).

## Commit & Pull Request Guidelines
- Follow Conventional Commit style: `feat(phase7): ...`, `fix(frontend): ...`, `fix(security): ...`
- PRs should include: clear summary, linked issue/PR number, test evidence, screenshots for UI changes.
- Do not mix unrelated changes in one PR.

## Security & Configuration Tips
- Never commit secrets; use environment-based config.
- Treat logs as non-sensitive by default; avoid writing tokens, IDs, or PII.
- Validate dependency bumps with full build + tests before merge.
- `docker-compose.yml` contains hardcoded local-dev secrets only — never for production.

## Design Context
### Users
- Primary audience is tourists visiting Alanya.
- Secondary audience includes local and repeat customers.
- Users want to find vehicles quickly, understand delivery options clearly, and reserve with confidence.

### Brand Personality
- Corporate, Trustworthy, Clean

### Aesthetic Direction
- Public frontend: corporate-minimal, light-only, desktop-first.
- Public frontend must not use shadcn components or shadcn design language.
- Public design must not feel like an admin dashboard or generic template.

### Design Principles
- Trust, pricing clarity, and process clarity should be visible immediately.
- Reservation flow should sit at the center of the information hierarchy.
- Public and admin design languages must stay separate.
- Simplicity should reduce friction without removing key information.

## Codex Sentinel Integration
### Security Checkpoints
- Planning checkpoint: before locking a plan, architecture, or task breakdown, run a security gap analysis.
- Risky-implementation checkpoint: when work touches authentication, authorization, tokens, secrets, middleware, outbound requests, file handling, CI, deployment, or other trust-boundary code, run a low-noise scoped risky-change review pass and surface only material concerns.
- Post-implementation checkpoint: when coding appears complete, offer a focused security review.
- Pre-release checkpoint: before release, deployment, or handoff, offer a stack-aware security check plan.

### Guardrails
- Treat security guidance as advisory-first unless the user explicitly asks for stricter gating.
- Never claim the codebase is secure, fully reviewed, or production-safe from a security perspective.
- Separate reviewed scope, unreviewed scope, assumptions, and tool-run status in every substantial security result.
- If the user declines review or test planning at the current stage, do not repeat the same offer until the stage changes.
- If the stack is unclear, fall back to common web-security guidance and say that stack inference is uncertain.

## Build, Test, and Development Commands
- Backend restore/build/test:
  - `dotnet restore backend/RentACar.sln --configfile backend/NuGet.Config`
  - `dotnet build backend/RentACar.sln --no-restore`
  - `dotnet test backend/RentACar.sln --no-build`
- Frontend install/build/test:
  - `corepack pnpm -C frontend install`
  - `corepack pnpm -C frontend build`
  - `corepack pnpm -C frontend test`
- Frontend local development:
  - `corepack pnpm -C frontend dev`

## Coding Style & Naming Conventions
- C# uses 4-space indentation, PascalCase for types/methods, camelCase for locals/parameters.
- TypeScript/React uses 2-space indentation, PascalCase for components, camelCase for variables/functions.
- Keep services explicit and small; avoid unnecessary abstractions.
- Prefer existing patterns in `Configuration/`, `Services/`, and `Contracts/` folders.
- Run lint/format before PR (`corepack pnpm -C frontend lint`).

## Testing Guidelines
- Backend: xUnit; place tests under `backend/tests/RentACar.Tests/Unit/...`.
- Frontend: Vitest + Testing Library; test files use `*.test.ts` / `*.test.tsx`.
- Add or update tests for any behavior change, especially auth, payments, reservations, and notifications.
- Keep tests deterministic (no real external network dependencies).

## Commit & Pull Request Guidelines
- Follow Conventional Commit style seen in history:
  - `feat(phase7): ...`
  - `fix(frontend): ...`
  - `fix(security): ...`
- PRs should include:
  - clear summary and scope,
  - linked issue/PR number,
  - test evidence (build/test output),
  - screenshots for UI changes.
- Do not mix unrelated changes in one PR.

## Security & Configuration Tips
- Never commit secrets; use environment-based config.
- Treat logs as non-sensitive by default; avoid writing tokens, IDs, or PII.
- Validate dependency bumps with full build + tests before merge.

## Design Context

### Users
- Primary audience is tourists visiting Alanya.
- Secondary audience includes local and repeat customers.
- Users want to find vehicles quickly, understand delivery options clearly, and reserve with confidence.

### Brand Personality
- Corporate
- Trustworthy
- Clean

### Aesthetic Direction
- Public frontend should be corporate-minimal.
- Default theme should be light only.
- Layout decisions should be desktop-first.
- Tablet and mobile should still look polished and complete.
- Public frontend must not use shadcn components or shadcn design language.
- Public design must not feel like an admin dashboard or generic template.

### Design Principles
- Trust, pricing clarity, and process clarity should be visible immediately.
- Reservation flow should sit at the center of the information hierarchy.
- Desktop-first should guide layout, without sacrificing tablet/mobile quality.
- Public and admin design languages must stay separate.
- Simplicity should reduce friction without removing key information.

## Codex Sentinel Integration
### Security Checkpoints
- Planning checkpoint: before locking a plan, architecture, or task breakdown, run a security gap analysis.
- Risky-implementation checkpoint: when work touches authentication, authorization, tokens, secrets, middleware, outbound requests, file handling, CI, deployment, or other trust-boundary code, run a low-noise scoped risky-change review pass and surface only material concerns.
- Post-implementation checkpoint: when coding appears complete, offer a focused security review.
- Pre-release checkpoint: before release, deployment, or handoff, offer a stack-aware security check plan.

### Guardrails
- Treat security guidance as advisory-first unless the user explicitly asks for stricter gating.
- Never claim the codebase is secure, fully reviewed, or production-safe from a security perspective.
- Separate reviewed scope, unreviewed scope, assumptions, and tool-run status in every substantial security result.
- If the user declines review or test planning at the current stage, do not repeat the same offer until the stage changes.
- If the stack is unclear, fall back to common web-security guidance and say that stack inference is uncertain.
