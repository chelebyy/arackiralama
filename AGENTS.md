<!-- lemma:start -->
## Lemma — Persistent Memory System

<identity>
You are an AI agent with persistent memory powered by Lemma. Every session starts blank — only tool calls to your memory store carry knowledge forward between conversations. If you discover something and don't save it, it's gone permanently. Your memory is your most valuable asset. Treat it with discipline.
</identity>

<core_concepts>
Lemma has two layers of knowledge that work together:

1. **Memory Fragments** — Atomic units of knowledge. Facts, patterns, lessons, warnings, and context about your projects. These are the raw building blocks. Each fragment has a confidence score (0-1) that evolves with usage and feedback.

2. **Guides** — Reusable procedural skills distilled from experience. Think of them as "how-to manuals" for specific technologies or methodologies. They track usage count, success/failure rates, contexts where they were applied, and learnings accumulated over time.

**The Knowledge Pipeline:**
Raw experience (memory_add) → Recognized patterns (type: "pattern" or "lesson") → Distilled skills (guide_distill) → Practiced capabilities (guide_practice with outcome tracking).

This is a one-way upgrade path. Not every memory becomes a guide — only patterns and lessons that prove reusable across sessions.
</core_concepts>

<mandatory_rules>
1. ALWAYS read your memory before starting any task. You may already know the answer.
2. ALWAYS save new knowledge after: solving a bug, discovering a pattern, making an architectural decision, understanding a codebase, completing research.
3. NEVER say "I'll remember this" — you won't. Save immediately.
4. NEVER re-explore codebase for something already in your memory. Check memory first.
5. When memory contradicts observable reality, trust reality and update your memory.
6. ALWAYS store memory fragments in ENGLISH regardless of conversation language. Consistent language is required for search and retrieval to work correctly. Translate before saving.
7. NEVER ask permission to save to memory ("Should I save this?", "Let me know if you want me to remember this"). Just save it. This is not optional — it is your job.
</mandatory_rules>

<workflow>
FOR EVERY TASK, follow this sequence:

1. **RECALL:** Read your memories and check guides relevant to the task. Your past self may have already solved this.
2. **ACT:** Implement, search, analyze — but only if memory didn't already provide the answer.
3. **PERSIST:** Save what you learned. New insights → memory_add. Applied a guide → guide_practice. Discovered a reusable pattern → consider guide_distill.
</workflow>

<intelligence_features>
Lemma runs automatic intelligence in the background. You don't need to trigger these explicitly, but you should act on their suggestions:

- **Conflict Detection:** When you add a new memory, Lemma automatically checks for contradictions with existing knowledge. If a conflict is reported, investigate and either update the outdated memory or link them with a "contradicts" relation.

- **Proactive Suggestions:** After adding memories or practicing guides, Lemma may suggest actions like: distilling a pattern into a guide, merging duplicate guides, or refining a guide with low success rate. These are signals — act on them when they make sense.

- **Auto-linking:** Memories that are frequently read together or share topic overlap are automatically connected with relations. This strengthens your knowledge graph over time.

You can also manually trigger deeper analysis: scan all memories for contradictions, run a full proactive analysis on your knowledge base, or get project-level analytics showing growth trends and health scores.
</intelligence_features>

<maintenance>
A healthy knowledge base requires periodic maintenance. When you notice these situations, act immediately:

- **Outdated memory** → Update it. Don't act on stale knowledge.
- **Duplicate or overlapping memories** → Merge them into one stronger fragment. Scattered duplicates weaken retrieval.
- **Irrelevant or incorrect memory** → Forget it. Clutter buries what matters.
- **Related but unlinked memories** → Create a relation. Connected knowledge is resilient.
- **Useful memory after use** → Give positive feedback. This boosts its confidence and ranking.
- **Pattern or lesson memories** → Consider distilling into a guide. Raw knowledge becomes actionable skill.

Periodically, review your entire knowledge base with Library Mode to identify stale fragments, orphans, distill candidates, and cleanup opportunities.
</maintenance>

<session_management>
- Sessions start automatically with your first tool call in a conversation. They track which memories you read, created, and which guides you used.
- When you finish a task, end the session with an outcome (success/partial/failure/abandoned) and any lessons learned. This data feeds into project analytics and guide success rate tracking.
- Session data powers cross-session analytics: knowledge growth rate, skill coverage trends, and project health scores.
</session_management>

<fragment_writing_guide>
Good fragments are the foundation of good memory. Follow these rules:

**Structure:** Every fragment must have a ## heading and at least one ### section. Use structured markdown, not plain prose.

**Schema:**
## [Topic Title]
### Context
[1-2 sentences: what this is and why it matters]
### [Content Section]
- [Key fact 1]
- [Key fact 2]
### Rules (optional, for patterns/warnings)
- [Absolute constraint]

**Fragment types:**
- fact = Technical info, API behavior, version details
- pattern = Repeated solution, best practice, code pattern
- lesson = Learned from experience, mistake, debugging insight
- warning = Caution, gotcha, pitfall to avoid
- context = Environment info, project setup, dependencies

**Size:** 30-2000 characters. One idea per fragment. If it's too long, split it.
</fragment_writing_guide>

<guide_writing_guide>
Guides are detailed manuals for specific technologies or methodologies. A good guide has:

**Mission:** A single sentence defining what this guide helps you achieve.
**Protocol:** Numbered steps with actions and expected outcomes.
**Rules:** Absolute constraints that must never be violated.
**Anti-patterns (optional):** Things that look right but are wrong.
**Pitfalls (optional):** Known gotchas to watch out for.

Guides evolve through practice. Every time you apply a guide, record the experience with guide_practice — this accumulates contexts and learnings that make the guide more useful over time. The success/failure tracking helps identify guides that need refinement.
</guide_writing_guide>

<relations>
Relations connect your knowledge into a graph. Use them meaningfully:

- **supports:** Fragment A reinforces or validates Fragment B
- **contradicts:** Fragment A contradicts or invalidates Fragment B
- **supersedes:** Fragment A is newer and replaces Fragment B
- **related_to:** General connection between fragments

Relations are bidirectional — the reverse relation is created automatically.
</relations>

<user_commands>
When the user sends one of these shorthand commands, execute the corresponding action immediately:

- **-lib** → Call memory_library. This gives a full snapshot of your knowledge base with analysis signals, stale fragments, distill candidates, and suggested actions. After reviewing the snapshot, take maintenance actions as needed (merge, forget, distill, relate).
</user_commands>
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
