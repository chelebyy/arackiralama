# Architecture

**Analysis Date:** 2026-03-14

## Pattern Overview

**Overall:** Modular Monolith with Clean Architecture (Onion Architecture)

**Key Characteristics:**
- Separation of concerns with clear domain boundaries
- Backend follows Clean Architecture pattern with Core, Infrastructure, and API layers
- Frontend uses Next.js App Router with server components
- Background worker for asynchronous processing
- Database-first approach with PostgreSQL and Redis

## Layers

**Domain Layer (RentACar.Core):**
- Purpose: Contains business entities, enums, interfaces, and domain logic
- Location: `backend/src/RentACar.Core/`
- Contains: Entities, Value Objects, Domain Services, Interfaces
- Depends on: None (innermost layer)
- Used by: Infrastructure, API, Worker

**Infrastructure Layer (RentACar.Infrastructure):**
- Purpose: Data access, external service integrations, and persistence
- Location: `backend/src/RentACar.Infrastructure/`
- Contains: DbContext, Migrations, Repositories, External service clients
- Depends on: Core layer
- Used by: API, Worker

**API Layer (RentACar.API):**
- Purpose: REST API endpoints, controllers, middleware, and presentation logic
- Location: `backend/src/RentACar.API/`
- Contains: Controllers, DTOs, Middleware, Configuration
- Depends on: Core and Infrastructure layers
- Used by: External clients, Frontend

**Background Worker (RentACar.Worker):**
- Purpose: Asynchronous job processing and background tasks
- Location: `backend/src/RentACar.Worker/`
- Contains: Background services, job processors
- Depends on: Core and Infrastructure layers
- Used by: Internal scheduling and task execution

**Frontend Layer (frontend/):**
- Purpose: Admin dashboard UI with Next.js and React
- Location: `frontend/`
- Contains: React components, pages, hooks, utilities
- Depends on: Backend API
- Used by: Admin users

## Data Flow

**API Request Flow:**
1. HTTP request arrives at API endpoint
2. Middleware pipeline processes request (CorrelationId → RequestLogging → Culture → ErrorHandling → Auth → RateLimiter)
3. Controller receives request and validates input
4. Service layer processes business logic using Core domain entities
5. Infrastructure layer interacts with database or external services
6. Response is formatted and returned to client

**Background Job Flow:**
1. Job is scheduled or triggered
2. Worker picks up job from persistent queue
3. Job processor executes business logic
4. Results are stored or notifications sent
5. Job completion is recorded

**State Management:**
- Backend: Entity Framework with PostgreSQL for ACID transactions
- Caching: Redis for short-lived data and rate limiting
- Frontend: Zustand for state management with server components

## Key Abstractions

**Entity Base Class:**
- Purpose: Base entity with common properties (GUID Id, CreatedAt, UpdatedAt)
- Examples: `backend/src/RentACar.Core/Entities/BaseEntity.cs`
- Pattern: Abstract base class inheritance

**Dependency Injection:**
- Purpose: Loose coupling between layers
- Examples: `backend/src/RentACar.API/Program.cs` with service registration
- Pattern: Constructor injection with interfaces

**Middleware Pipeline:**
- Purpose: Cross-cutting concerns handling
- Examples: `backend/src/RentACar.API/Configuration/MiddlewareConfiguration.cs`
- Pattern: Pipeline processing with order-dependent middleware

## Entry Points

**Backend API Entry Point:**
- Location: `backend/src/RentACar.API/Program.cs`
- Triggers: HTTP requests on port 5135 (development) or 5000 (Docker)
- Responsibilities: Application bootstrapping, service registration, middleware configuration

**Background Worker Entry Point:**
- Location: `backend/src/RentACar.Worker/Program.cs`
- Triggers: Application startup or scheduled tasks
- Responsibilities: Worker service initialization and job processing

**Frontend Entry Point:**
- Location: `frontend/app/layout.tsx`
- Triggers: Browser navigation or API requests
- Responsibilities: Root layout, theme configuration, routing

## Error Handling

**Strategy:** Centralized error handling with middleware

**Patterns:**
- Global exception middleware in API layer
- Custom exception types for business errors
- Logging of errors with correlation IDs
- Graceful error responses to clients

## Cross-Cutting Concerns

**Logging:** Structured logging with correlation IDs
**Validation:** DTO validation with FluentValidation
**Authentication:** JWT with RBAC policies (AdminOnly, SuperAdminOnly)
**Rate Limiting:** Policy-based rate limiting (Global: 100/min, Strict: 5/min, Payment: 10/min, Standard: 30/min)

---

*Architecture analysis: 2026-03-14*