# GEMINI.md - Araç Kiralama Platformu Configuration

This repository contains an Enterprise-grade Rent A Car Platform (focused on Alanya) with a .NET 10 backend and a Next.js 16 frontend.

## Project Overview

- **Backend**: .NET 10 Solution (`backend/RentACar.sln`) following clean architecture.
  - `src/RentACar.API`: ASP.NET Core API controllers, middleware, and service wiring.
  - `src/RentACar.Core`: Domain entities, interfaces, constants, and business logic.
  - `src/RentACar.Infrastructure`: Data access (EF Core), background jobs, and external service implementations.
  - `src/RentACar.Worker`: Background processing host.
  - `tests/RentACar.Tests`: xUnit unit and integration tests.
- **Frontend**: Next.js 16 (App Router) + React 19 dashboard and public site.
  - `app/`: Next.js App Router pages and layouts.
  - `components/`: UI components (shadcn/ui for admin, custom for public).
  - `hooks/`: Custom React hooks.
  - `lib/`: Utility functions and API clients.
- **Database**: PostgreSQL (Primary) and Redis (Cache/Hold mechanism).
- **Architecture**: Domain-Driven Design (DDD) principles, modular organization, and containerized deployment.

## Building and Running

### Prerequisites
- .NET SDK 10.0+
- Node.js 22+ (pnpm 9+)
- Docker & Docker Compose

### Backend
```bash
cd backend
dotnet restore RentACar.sln
dotnet build RentACar.sln
dotnet test RentACar.sln
dotnet run --project src/RentACar.API
```

### Frontend
```bash
cd frontend
pnpm install
pnpm dev   # Development
pnpm build # Production build
pnpm test  # Run tests
```

### Docker
```bash
# Start all services (Database, Redis, API, Worker)
cd backend
docker compose up --build
```

## Development Conventions

### Coding Style
- **C#**: 4-space indentation, PascalCase for types/methods, camelCase for locals/parameters. Use File-Scoped Namespaces.
- **TypeScript/React**: 2-space indentation, PascalCase for components, camelCase for variables/functions. Use functional components and hooks.
- **Indentation**: Rigorously follow the indentation rules for each language.

### Testing
- **Backend**: xUnit; tests located in `backend/tests/RentACar.Tests/Unit/...`.
- **Frontend**: Vitest + Testing Library; files named `*.test.ts` or `*.test.tsx`.
- **Mandate**: Add unit tests for all new features and bug fixes.

### Design Context
- **Primary Audience**: Tourists visiting Alanya.
- **Secondary Audience**: Local and repeat customers.
- **User Goals**: Find vehicles quickly, understand delivery and pickup options clearly, compare trust signals easily, and move to reservation without friction.
- **Brand Personality**: Corporate, Trustworthy, Clean.
- **Public Frontend Direction**: Corporate-minimal and clearly separated from the admin dashboard look and feel.
- **Theme Mode**: Light theme only for the public frontend.
- **Responsive Strategy**: Desktop-first decisions should lead the layout, but tablet and mobile views must still look clean and complete.
- **Public Constraint**: DO NOT use shadcn components or shadcn visual language for the public frontend.
- **Admin Constraint**: Admin design is out of scope for public-facing design changes; shadcn usage may remain in the admin panel.

### Public Frontend Principles
1. Trust should be visible immediately through clear pricing, transparent process cues, delivery information, and contact confidence.
2. Reservation flow should shape the hierarchy of the public pages.
3. Desktop-first composition should not degrade the experience on tablet and mobile.
4. Public and admin surfaces must feel intentionally different.
5. Simplicity should support decision-making, not hide useful information.

### Security (Codex Sentinel)
- **Hardened Boundaries**: Rigorous validation on authentication, payments, and reservations.
- **Secrets**: NEVER commit secrets to the repository. Use environment variables.
- **Audit**: Log critical actions (auth, payments, status changes) but avoid PII in logs.

## CI/CD Workflow
- **GitHub Actions**: `.github/workflows/ci.yml` handles:
  - Backend: build, test (with coverage), Docker image build.
  - Frontend: lint, type check, test, build.
  - Deployment: Pushes Docker images to GHCR on `main` branch push.

## Key Documentation
- `docs/01_PRD_ENTERPRISE_FULL.md`: Product Requirements.
- `docs/03_TDD_ENTERPRISE_FULL.md`: Technical Design.
- `docs/09_Implementation_Plan.md`: Current Roadmap.
- `AGENTS.md`: Comprehensive guidelines and agent instructions.
