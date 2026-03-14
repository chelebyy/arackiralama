# External Integrations

**Analysis Date:** 2026-03-14

## APIs & External Services

**Payment Processing:**
- Iyzico - Turkish payment gateway integration
  - SDK/Client: Custom implementation in `backend/src/RentACar.Infrastructure/Services/Payments/IyzicoPaymentProvider.cs`
  - Auth: API Key and Secret Key (configurable in `PaymentOptions.Iyzico`)
  - Environment: Sandbox mode for development, production available
  - Features: 3DS authentication simulation, payment intents, pre-authorization

**Mock Payment Provider:**
- Internal mock service for testing
  - Implementation: `backend/src/RentACar.Infrastructure/Services/Payments/MockPaymentProvider.cs`
  - Webhook simulation: Mock webhook processing for payment lifecycle

## Data Storage

**Databases:**
- PostgreSQL 18 - Primary relational database
  - Connection: `ConnectionStrings__DefaultConnection` environment variable
  - Client: Entity Framework Core with Npgsql provider
  - Schema: Clean architecture with migrations in `backend/src/RentACar.Infrastructure/Data/Migrations/`

**File Storage:**
- Local filesystem only (no external file storage detected)

**Caching:**
- Redis 7.4 - Distributed caching and rate limiting
  - Connection: `Redis__ConnectionString` environment variable
  - Client: StackExchange.Redis
  - Use cases: Session management, rate limiting, reservation hold TTL

## Authentication & Identity

**Auth Provider:**
- Custom JWT implementation
  - Implementation: ASP.NET Core JWTBearer authentication
  - Issuer: `Jwt__Issuer` configuration
  - Audience: `Jwt__Audience` configuration
  - Secret: `Jwt__Secret` (minimum 32 characters)
  - Policies: AdminOnly, SuperAdminOnly RBAC

## Monitoring & Observability

**Error Tracking:**
- Not detected (no external error tracking service configured)

**Logs:**
- File-based logging with Microsoft.Extensions.Logging
- Structured logging in JSON format

## CI/CD & Deployment

**Hosting:**
- Docker containerization for all services
- Docker Compose for local development
- Production deployment likely via container orchestration (Kubernetes/Docker Swarm)

**CI Pipeline:**
- Not detected (no CI configuration files found)

## Environment Configuration

**Required env vars:**
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string
- `Redis__ConnectionString` - Redis connection string
- `Jwt__Secret` - JWT signing secret (minimum 32 characters)
- `Payment__Provider` - Payment provider selection (Mock/Iyzico)

**Secrets location:**
- appsettings.json (development)
- Environment variables (production)

## Webhooks & Callbacks

**Incoming:**
- Payment webhook endpoints
  - `backend/src/RentACar.API/Controllers/PaymentsController.cs`
  - `backend/src/RentACar.API/Services/QueuedPaymentWebhookHostedService.cs`

**Outgoing:**
- Payment provider callbacks to Iyzico
  - 3DS redirect URLs
  - Webhook notifications

---

*Integration audit: 2026-03-14*