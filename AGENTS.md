# Repository Guidelines

**Generated:** 2026-04-23
**Stack:** .NET 10 + PostgreSQL + Redis | Next.js 16 + React 19 + TypeScript
**Pattern:** Clean Architecture (API/Core/Infrastructure/Worker)

Araç Kiralama is a Turkish car-rental platform for tourists (primary) and locals (secondary) in Alanya. Phase plans and the execution log live in `docs/09_Implementation_Plan.md` and `docs/10_Execution_Tracking.md`.

This file is the single source of project instructions for all coding agents; `CLAUDE.md` imports it.

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
| Add UI component | `frontend/components/ui/` | shadcn/ui (admin only) + custom in `frontend/components/` |
| Update translations | `frontend/i18n/messages/` | ar, de, en, ru, tr |

## Backend Conventions
- `RentACar.Core` currently references EF Core for `IApplicationDbContext` (`DbSet<T>` and `ChangeTracker`); `RentACar.Infrastructure` holds the database implementation (Npgsql), repositories, external services, Redis, and migrations under `Data/Migrations/`.
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

Frontend commands explicitly select pnpm 10.34.7.
```bash
# Backend (from repo root)
dotnet restore backend/RentACar.sln --configfile backend/NuGet.Config
dotnet build backend/RentACar.sln --no-restore
dotnet test backend/RentACar.sln --no-build
dotnet run --project backend/src/RentACar.API

# Frontend
corepack pnpm@10.34.7 -C frontend install
corepack pnpm@10.34.7 -C frontend dev      # local dev server
corepack pnpm@10.34.7 -C frontend build
corepack pnpm@10.34.7 -C frontend test     # vitest unit tests
corepack pnpm@10.34.7 -C frontend lint     # eslint
corepack pnpm@10.34.7 -C frontend exec playwright install --with-deps chromium # first E2E setup
# Start the local web/API stack separately; E2E_BASE_URL defaults to http://localhost:3000.
corepack pnpm@10.34.7 -C frontend exec playwright test # see frontend/playwright.config.ts

# Docker (full local stack: Postgres, Redis, API, Worker, Web, upload-permissions init)
cd backend && docker compose up --build
```

### Run a single test
```bash
# backend: filter by fully qualified name
dotnet test backend/RentACar.sln --no-build --filter "FullyQualifiedName~RentACar.Tests.Unit.SomeNamespace"

# frontend: pass a path pattern
corepack pnpm@10.34.7 -C frontend test -- path/to/file.test.tsx
```

## CI Pipeline
- `.github/workflows/ci.yml`: backend unit/integration tests, frontend typecheck/lint/test/build, and API/Worker Docker builds run in parallel; successful pushes to `main` then publish images to GHCR.
- `.github/workflows/codeql.yml`: security scanning for C# and JS/TS

## Coding Style & Naming Conventions
- C#: 4-space indent, PascalCase types/methods, camelCase locals/parameters.
- TypeScript/React: 2-space indent, PascalCase components, camelCase variables/functions.
- Keep services explicit and small; avoid unnecessary abstractions.
- Prefer existing patterns in `Configuration/`, `Services/`, and `Contracts/` folders.
- Run lint/format before PR (`corepack pnpm@10.34.7 -C frontend lint`).

## Testing Guidelines
- Backend: xUnit; place tests under `backend/tests/RentACar.Tests/Unit/...`.
- Frontend: Vitest + Testing Library; test files use `*.test.ts` / `*.test.tsx`.
- Add or update tests for any behavior change, especially auth, payments, reservations, and notifications.
- Keep tests deterministic (no real external network dependencies).

## Commit & Pull Request Guidelines
- Follow Conventional Commit style: `feat(phase7): ...`, `fix(frontend): ...`, `fix(security): ...`
- PRs should include: clear summary and scope, linked issue/PR number, test evidence (build/test output), screenshots for UI changes.
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

### Purpose
This repository uses Codex-Sentinel as a stage-aware cybersecurity guidance suite for Codex.

### Required Checkpoints
- Planning checkpoint: before locking a plan, task list, or architecture, run a security gap analysis.
- Risky-implementation checkpoint: when work touches authentication, authorization, tokens, secrets, middleware, outbound requests, file handling, CI, deployment, or other trust-boundary code, run a low-noise scoped risky-change review pass and surface only material concerns.
- Post-implementation checkpoint: when coding appears complete, offer a focused security review.
- Pre-release checkpoint: before release, handoff, or "done" confirmation, offer a stack-aware security test/check plan.

### Guardrails
- Treat the suite as advisory-first unless the user explicitly asks for stronger gating.
- Never claim the repository is secure, fully reviewed, or production-safe from a security perspective.
- Separate reviewed areas, unreviewed areas, assumptions, and tool-run status in every substantial report.
- If the user declines a security review or test/check plan in the current stage, do not repeat the same offer until the stage changes.
- If the stack is unclear, fall back to common web-security guidance and say that the stack inference is uncertain.

### Authoring Defaults
- Keep repository-facing skill files, metadata, docs, examples, and validation artifacts in English unless a localized deliverable is explicitly requested.
- Keep user-facing assistant responses in the user's configured Codex language when that signal is available. If it is unavailable, follow the active conversation language and fall back to English.
- Do not add inline or block comments in source code unless the user explicitly asks for them.
