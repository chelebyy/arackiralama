# Codebase Structure

**Analysis Date:** 2026-03-14

## Directory Layout

```
Araç Kiralama/
├── backend/                          # .NET 10.0 backend application
│   ├── src/                          # Source code
│   │   ├── RentACar.Core/            # Domain layer
│   │   ├── RentACar.Infrastructure/   # Data and external service layer
│   │   ├── RentACar.API/             # REST API presentation layer
│   │   └── RentACar.Worker/          # Background worker
│   ├── tests/                       # Test projects
│   │   └── RentACar.Tests/           # xUnit test project
│   │       ├── Unit/                # Unit tests
│   │       └── Integration/         # Integration tests
│   └── docker-compose.yml           # Local development environment
├── frontend/                        # Next.js admin dashboard
│   ├── app/                        # Next.js app router
│   │   ├── (admin)/                # Admin panel routes
│   │   │   └── dashboard/          # Dashboard pages
│   │   ├── layout.tsx              # Root layout
│   │   └── not-found.tsx           # 404 handler
│   ├── components/                 # Reusable components
│   │   ├── layout/                # Header, sidebar, logo
│   │   ├── theme-customizer/       # Theme configuration
│   │   └── ui/                    # shadcn/ui components
│   ├── hooks/                      # Custom React hooks
│   ├── lib/                       # Utilities and helpers
│   └── public/                    # Static assets
└── .planning/                     # Planning and analysis documents
```

## Directory Purposes

**backend/src/RentACar.Core:**
- Purpose: Domain layer containing business logic and entities
- Contains: Entities, enums, interfaces, domain services
- Key files: `Entities/BaseEntity.cs`, domain-specific entities

**backend/src/RentACar.Infrastructure:**
- Purpose: Data access and external service integrations
- Contains: DbContext, repositories, external service clients, migrations
- Key files: `DbContext/ApplicationDbContext.cs`, `Repositories/`

**backend/src/RentACar.API:**
- Purpose: REST API endpoints and presentation logic
- Contains: Controllers, DTOs, middleware, configuration
- Key files: `Controllers/`, `Configuration/`

**backend/src/RentACar.Worker:**
- Purpose: Background job processing
- Contains: Background services, job processors
- Key files: `Services/`, `Program.cs`

**backend/tests/RentACar.Tests:**
- Purpose: Test suite for backend
- Contains: Unit and integration tests
- Key files: `Unit/`, `Integration/`, test fixtures

**frontend/app:**
- Purpose: Next.js app router with page components
- Contains: Page components, layouts, routing
- Key files: `layout.tsx`, `(admin)/dashboard/`

**frontend/components:**
- Purpose: Reusable UI components
- Contains: shadcn/ui components, custom components
- Key files: `ui/`, `layout/`

## Key File Locations

**Entry Points:**
- Backend API: `backend/src/RentACar.API/Program.cs`
- Background Worker: `backend/src/RentACar.Worker/Program.cs`
- Frontend: `frontend/app/layout.tsx`

**Configuration:**
- Backend: `backend/src/RentACar.API/Configuration/`
- Frontend: `frontend/next.config.ts`, `frontend/vitest.config.ts`

**Core Logic:**
- Backend domain: `backend/src/RentACar.Core/`
- Backend infrastructure: `backend/src/RentACar.Infrastructure/`
- Frontend components: `frontend/components/`

**Testing:**
- Backend tests: `backend/tests/RentACar.Tests/`
- Frontend tests: `frontend/**/*.test.{ts,tsx}`

## Naming Conventions

**Files:**
- C# files: `PascalCase.cs` (e.g., `UserService.cs`)
- TypeScript files: `PascalCase.tsx` (e.g., `DashboardPage.tsx`)
- Test files: `FileName.Tests.cs` or `*.test.tsx`

**Directories:**
- Feature-based: `Auth/`, `Vehicles/`, `Reservations/`
- Layer-based: `Controllers/`, `Services/`, `Repositories/`
- Component-based: `components/ui/`, `components/layout/`

## Where to Add New Code

**New Feature (Backend):**
- Primary code: `backend/src/RentACar.Core/` (domain entities/interfaces) → `backend/src/RentACar.Infrastructure/` (implementations) → `backend/src/RentACar.API/` (controllers)
- Tests: `backend/tests/RentACar.Tests/Unit/` or `backend/tests/RentACar.Tests/Integration/`

**New Component (Frontend):**
- Implementation: `frontend/components/` (create new subdirectory)
- Pages: `frontend/app/(admin)/dashboard/` (create new route)
- Tests: `frontend/**/*.test.tsx`

**Utilities:**
- Shared helpers: `backend/src/RentACar.Core/` or `frontend/lib/`
- Common services: `backend/src/RentACar.Infrastructure/`

## Special Directories

**.planning/codebase/:**
- Purpose: Codebase analysis and planning documents
- Generated: Yes (by GSD agents)
- Committed: Yes (part of repository)

**backend/.dotnet/:**
- Purpose: .NET tooling and package cache
- Generated: Yes
- Committed: No (excluded in .gitignore)

**frontend/.next/:**
- Purpose: Next.js build output and cache
- Generated: Yes
- Committed: No (excluded in .gitignore)

---

*Structure analysis: 2026-03-14*