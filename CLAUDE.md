# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **Full project guidelines live in [`AGENTS.md`](./AGENTS.md)** (architecture, conventions, design rules, security checkpoints). This file covers session-tooling rules and the day-to-day commands you need to be productive quickly.

---

## What This Repo Is

Araç Kiralama — a Turkish car-rental platform for tourists (primary) and locals (secondary) in Alanya.

- `backend/` — .NET 10 + PostgreSQL + Redis, Clean Architecture (API / Core / Infrastructure / Worker)
- `frontend/` — Next.js 16 App Router + React 19 + TypeScript, i18n (ar/de/en/ru/tr), public + admin route groups
- `docs/09_Implementation_Plan.md`, `docs/10_Execution_Tracking.md` — phase plans and execution log
- `docker-compose.yml` — local Postgres + Redis + API stack

## Day-to-Day Commands

### Backend
```bash
# from repo root
dotnet restore backend/RentACar.sln --configfile backend/NuGet.Config
dotnet build  backend/RentACar.sln --no-restore
dotnet test   backend/RentACar.sln --no-build
dotnet run --project backend/src/RentACar.API

# full local stack (Postgres + Redis + API)
cd backend && docker compose up --build
```

### Frontend
```bash
corepack pnpm -C frontend install
corepack pnpm -C frontend dev      # local dev server
corepack pnpm -C frontend build
corepack pnpm -C frontend test     # vitest unit tests
corepack pnpm -C frontend lint     # eslint
corepack pnpm -C frontend e2e      # playwright (see frontend/playwright.config.ts)
```

### Run a single test
```bash
# backend: filter by fully qualified name
dotnet test backend/RentACar.sln --no-build --filter "FullyQualifiedName~RentACar.Tests.Unit.SomeNamespace"

# frontend: pass a path pattern
corepack pnpm -C frontend test -- path/to/file.test.tsx
```

## Big-Picture Architecture

### Backend — Clean Architecture
- **`RentACar.Core`** — entities, interfaces, enums, constants. No external deps.
- **`RentACar.Infrastructure`** — EF Core (Npgsql), repositories, external services, Redis, migrations under `Data/Migrations/`.
- **`RentACar.API`** — controllers (22), middleware, auth, `Services/` (20 application services, non-standard placement), `Contracts/` DTOs per domain (Fleet/Auth/Pricing/Reservations), `Specifications/` for CQRS-style queries.
- **`RentACar.Worker`** — minimal; background polling lives in `Worker.cs` (30s loop).
- `Program.cs` wires DI via `AddApiApplicationServices()` extension.

### Frontend — App Router
- Route groups: `(admin)/dashboard/(auth)/...` (requires auth), `(admin)/dashboard/(guest)/...` (login), `(public)/[locale]/...` (i18n).
- Entry point `app/page.tsx` hardcodes `redirect("/tr")`.
- `app/api/` — internal proxy route handlers.
- `components/ui/` — shadcn/ui (admin only). `components/` — custom.
- `lib/` — API clients, auth utilities. `i18n/messages/` — ar, de, en, ru, tr.
- **Design split is hard-enforced**: public pages must NOT use shadcn/ui or admin design language (corporate-minimal, light-only, desktop-first). Admin/dashboard may.

## Where to Make Changes
| Task | Location |
|------|----------|
| Add API endpoint | `backend/src/RentACar.API/Controllers/` |
| Add domain entity | `backend/src/RentACar.Core/Entities/` |
| Add EF migration | `backend/src/RentACar.Infrastructure/Data/Migrations/` |
| Add background job | `backend/src/RentACar.Worker/Worker.cs` |
| Add public page | `frontend/app/(public)/[locale]/` |
| Add admin page | `frontend/app/(admin)/dashboard/(auth)/` |
| Add UI component | `frontend/components/ui/` (admin) or `frontend/components/` (custom) |
| Update translations | `frontend/i18n/messages/` |

## Conventions
- **Commits**: Conventional Commits — `feat(phase7): ...`, `fix(frontend): ...`, `fix(security): ...`.
- **PRs**: clear summary + linked issue/PR + test evidence + screenshots for UI. No mixed concerns.
- **C#**: 4-space indent, PascalCase types/methods, camelCase locals.
- **TypeScript/React**: 2-space indent, PascalCase components, camelCase variables.
- **Tests**: backend xUnit (`backend/tests/RentACar.Tests/Unit/...`); frontend Vitest + Testing Library (`*.test.ts` / `*.test.tsx`). Always deterministic — no real network.
- **Secrets**: never commit; environment-based. `docker-compose.yml` is local-dev only.

## CI
- `.github/workflows/ci.yml` — backend build/test → frontend lint/test/build → docker build → GHCR push (main only).
- `.github/workflows/dependabot-auto-merge.yml` — auto-merges patch/minor Dependabot PRs.
- `.github/workflows/codeql.yml` — security scanning (C# + JS/TS).

---

## context-mode — MANDATORY routing rules

You have context-mode MCP tools available. These rules are NOT optional — they protect your context window from flooding. A single unrouted command can dump 56 KB into context and waste the entire session.

### BLOCKED commands — do NOT attempt these

#### curl / wget — BLOCKED
Any Bash command containing `curl` or `wget` is intercepted and replaced with an error message. Do NOT retry.
Instead use:
- `ctx_fetch_and_index(url, source)` to fetch and index web pages
- `ctx_execute(language: "javascript", code: "const r = await fetch(...)")` to run HTTP calls in sandbox

#### Inline HTTP — BLOCKED
Any Bash command containing `fetch('http`, `requests.get(`, `requests.post(`, `http.get(`, or `http.request(` is intercepted and replaced with an error message. Do NOT retry with Bash.
Instead use:
- `ctx_execute(language, code)` to run HTTP calls in sandbox — only stdout enters context

#### WebFetch — BLOCKED
WebFetch calls are denied entirely. The URL is extracted and you are told to use `ctx_fetch_and_index` instead.
Instead use:
- `ctx_fetch_and_index(url, source)` then `ctx_search(queries)` to query the indexed content

### REDIRECTED tools — use sandbox equivalents

#### Bash (>20 lines output)
Bash is ONLY for: `git`, `mkdir`, `rm`, `mv`, `cd`, `ls`, `npm install`, `pip install`, and other short-output commands.
For everything else, use:
- `ctx_batch_execute(commands, queries)` — run multiple commands + search in ONE call
- `ctx_execute(language: "shell", code: "...")` — run in sandbox, only stdout enters context

#### Read (for analysis)
If you are reading a file to **Edit** it → Read is correct (Edit needs content in context).
If you are reading to **analyze, explore, or summarize** → use `ctx_execute_file(path, language, code)` instead. Only your printed summary enters context. The raw file content stays in the sandbox.

#### Grep (large results)
Grep results can flood context. Use `ctx_execute(language: "shell", code: "grep ...")` to run searches in sandbox. Only your printed summary enters context.

### Tool selection hierarchy

1. **GATHER**: `ctx_batch_execute(commands, queries)` — Primary tool. Runs all commands, auto-indexes output, returns search results. ONE call replaces 30+ individual calls.
2. **FOLLOW-UP**: `ctx_search(queries: ["q1", "q2", ...])` — Query indexed content. Pass ALL questions as array in ONE call.
3. **PROCESSING**: `ctx_execute(language, code)` | `ctx_execute_file(path, language, code)` — Sandbox execution. Only stdout enters context.
4. **WEB**: `ctx_fetch_and_index(url, source)` then `ctx_search(queries)` — Fetch, chunk, index, query. Raw HTML never enters context.
5. **INDEX**: `ctx_index(content, source)` — Store content in FTS5 knowledge base for later search.

### Subagent routing

When spawning subagents (Agent/Task tool), the routing block is automatically injected into their prompt. Bash-type subagents are upgraded to general-purpose so they have access to MCP tools. You do NOT need to manually instruct subagents about context-mode.

### Output constraints

- Keep responses under 500 words.
- Write artifacts (code, configs, PRDs) to FILES — never return them as inline text. Return only: file path + 1-line description.
- When indexing content, use descriptive source labels so others can `ctx_search(source: "label")` later.

### ctx commands

| Command | Action |
|---------|--------|
| `ctx stats` | Call the `ctx_stats` MCP tool and display the full output verbatim |
| `ctx doctor` | Call the `ctx_doctor` MCP tool, run the returned shell command, display as checklist |
| `ctx upgrade` | Call the `ctx_upgrade` MCP tool, run the returned shell command, display as checklist |
