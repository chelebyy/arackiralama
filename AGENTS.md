# Repository Guidelines

## Project Structure & Module Organization
- `backend/`: .NET 10 solution (`RentACar.sln`).
- `backend/src/RentACar.API`: ASP.NET Core API controllers, service wiring, middleware.
- `backend/src/RentACar.Core`: domain entities, interfaces, constants.
- `backend/src/RentACar.Infrastructure`: EF Core, migrations, background jobs, provider implementations.
- `backend/src/RentACar.Worker`: background processing host.
- `backend/tests/RentACar.Tests`: xUnit unit/integration tests.
- `frontend/`: Next.js 16 + React 19 dashboard app (`app/`, `components/`, `hooks/`, `lib/`).
- `docs/`: execution notes and project documentation.

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
