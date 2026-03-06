# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a multi-language vehicle rental platform ("Araç Kiralama") targeting the Turkish market. The system is a monorepo with two main applications:

- **Backend**: .NET 10.0 REST API with Clean Architecture
- **Frontend**: Next.js 16 admin dashboard (public website to be added)

## Development Commands

### Backend (.NET)

```bash
cd backend

# Restore and build
dotnet restore RentACar.sln
dotnet build RentACar.sln

# Run API (development)
dotnet run --project src/RentACar.API

# Run tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Docker development (PostgreSQL, Redis, API, Worker)
docker compose up --build
```

**Backend Endpoints:**
- Health: `GET http://localhost:5135/api/v1/health` (dotnet run)
- Health: `GET http://localhost:5000/health` (Docker)
- PostgreSQL: `localhost:5433` (Docker)
- Redis: `localhost:6379` (Docker)

### Frontend (Next.js)

```bash
cd frontend

# Install dependencies
pnpm install

# Development server
pnpm dev

# Build for production
pnpm build

# Lint
pnpm lint

# Run tests
pnpm test

# Run tests with coverage
pnpm test:coverage

# Run tests in watch mode
pnpm test:watch
```

**Frontend runs at:** `http://localhost:3000`

## Architecture

### Backend Architecture (Clean/Onion)

```
src/
  RentACar.Core/          # Domain layer - entities, enums, interfaces
  RentACar.Infrastructure/ # Data layer - DbContext, migrations, security
  RentACar.API/           # Presentation layer - controllers, middleware, services
  RentACar.Worker/        # Background job processor
tests/
  RentACar.Tests/         # xUnit + FluentAssertions + Moq
```

**Key Patterns:**
- Entities inherit from `BaseEntity` (GUID Id, CreatedAt, UpdatedAt)
- Infrastructure registered via `DependencyInjection.AddInfrastructure()`
- Middleware pipeline: CorrelationId → RequestLogging → Culture → ErrorHandling → Auth → RateLimiter
- Rate limiting policies: Global (100/min), Strict (5/min), Payment (10/min), Standard (30/min)

### Frontend Architecture (Next.js App Router)

```
frontend/
  app/
    (admin)/dashboard/    # Admin panel routes
      (auth)/             # Authenticated pages
      (guest)/            # Login, register, error pages
  components/
    layout/               # Header, sidebar, logo
    theme-customizer/     # Theme configuration panel
    ui/                   # shadcn/ui components (99 components)
  hooks/                  # Custom React hooks
  lib/                    # Utilities (cn, compose-refs, fonts, themes)
```

**Tech Stack:**
- React 19 + TypeScript
- Tailwind CSS v4
- shadcn/ui components
- Zustand for state management
- React Hook Form + Zod for validation
- Recharts for charts
- TipTap for rich text editing

## Key Configuration Files

| File | Purpose |
|------|---------|
| `backend/docker-compose.yml` | Local PostgreSQL, Redis, API, Worker |
| `backend/RentACar.sln` | Solution with 4 projects + 1 test project |
| `frontend/package.json` | Dependencies and scripts |
| `frontend/vitest.config.ts` | Test configuration with v8 coverage |
| `frontend/tsconfig.json` | Path alias `@/*` maps to root |

## Database Migrations

```bash
cd backend

# Create migration (after model changes)
dotnet ef migrations add MigrationName \
  --project src/RentACar.Infrastructure \
  --startup-project src/RentACar.API

# Apply migrations
dotnet ef database update \
  --project src/RentACar.Infrastructure \
  --startup-project src/RentACar.API
```

## Required Configuration

Backend requires these environment variables (or appsettings.Development.json):

- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string
- `Redis__ConnectionString` - Redis connection string
- `Jwt__Secret` - Must be at least 32 characters

## Test Structure

**Backend:** xUnit with FluentAssertions
- Unit tests: `tests/RentACar.Tests/Unit/`
- Integration tests: `tests/RentACar.Tests/Integration/`
- In-memory DbContext for testing

**Frontend:** Vitest + Testing Library
- Test files: `**/*.test.{ts,tsx}` or `**/*.spec.{ts,tsx}`
- Coverage output: `./coverage/`

## Architecture Decisions (from ADR)

- **Modular Monolith**: Microservice-ready with clear domain boundaries
- **PostgreSQL**: ACID compliance, row-level locking for reservation overlap control
- **Redis**: Reservation hold TTL, rate limiting, short-lived caching
- **Background Worker**: Persistent job table pattern with `SELECT ... FOR UPDATE SKIP LOCKED`
- **JWT + RBAC**: AdminOnly and SuperAdminOnly policies
