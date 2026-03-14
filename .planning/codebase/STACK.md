# Technology Stack

**Analysis Date:** 2026-03-14

## Languages

**Primary:**
- C# 10.0 - Backend .NET 10.0 applications
- TypeScript 5.4+ - Frontend Next.js 16 applications

**Secondary:**
- SQL - PostgreSQL database queries and migrations
- JavaScript - Frontend React components and utilities

## Runtime

**Environment:**
- .NET 10.0 - Backend runtime
- Node.js 20+ - Frontend runtime (Next.js 16 requires Node.js 18+)

**Package Manager:**
- NuGet 6.0+ - .NET package management
- pnpm 10.31.0 - Frontend package management
- Lockfile: present (pnpm-lock.yaml)

## Frameworks

**Core:**
- ASP.NET Core 10.0 - Backend REST API framework
- Next.js 16.1.6 - Frontend React framework with App Router
- Entity Framework Core 10.0.3 - .NET ORM

**Testing:**
- xUnit 2.9+ - Backend unit and integration testing
- Vitest 2.1.5 - Frontend unit testing
- Testing Library - Frontend component testing

**Build/Dev:**
- Docker - Containerization for development and deployment
- Docker Compose - Multi-container orchestration
- ESLint - Frontend code linting
- Prettier - Frontend code formatting

## Key Dependencies

**Critical:**
- PostgreSQL 18 - Primary database
- Redis 7.4 - Caching and rate limiting
- JWT (JsonWebToken) - Authentication and authorization
- BCrypt.Net-Next 4.0.3 - Password hashing

**Infrastructure:**
- Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0 - PostgreSQL provider for EF Core
- StackExchange.Redis 2.11.8 - Redis client
- Stripe/Iyzico - Payment processing (Iyzico integration)

## Configuration

**Environment:**
- appsettings.json - Backend configuration
- .env files - Frontend environment variables
- ConnectionStrings__DefaultConnection - PostgreSQL connection string
- Redis__ConnectionString - Redis connection string
- Jwt__Secret - JWT signing secret (minimum 32 characters)

**Build:**
- backend/RentACar.sln - Solution file with 5 projects
- frontend/package.json - Frontend dependencies and scripts
- backend/docker-compose.yml - Development infrastructure configuration

## Platform Requirements

**Development:**
- Docker and Docker Compose
- .NET 10 SDK
- Node.js 20+
- PostgreSQL 18
- Redis 7.4

**Production:**
- Containerized deployment (Docker)
- PostgreSQL database
- Redis cache
- Reverse proxy (NGINX/Apache)

---

*Stack analysis: 2026-03-14*