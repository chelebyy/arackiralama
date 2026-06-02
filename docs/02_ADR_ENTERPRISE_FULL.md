# Architecture Decision Record (ADR)

Date: 2026-02-25 Status: Accepted

## 1. Backend Technology

Chosen: .NET SDK 10.0.103 + ASP.NET Core 10.0.3 (LTS) Reason: - Long-term support - Strong transaction &
concurrency handling - Mature ecosystem - Suitable for financial
integrations

## 2. Architecture Style

Chosen: Modular Monolith (Microservice-ready)

Rationale: - Reduced operational complexity - Clear domain boundaries -
Future service extraction possible

## 3. Database

Chosen: PostgreSQL

Rationale: - ACID compliance - Strong row-level locking - Advanced
indexing - JSONB support - Suitable for reservation overlap control

## 4. Cache Layer

Chosen: Redis

Used for: - Reservation hold TTL - Rate limiting - Short-lived caching

## 5. Async Processing

Chosen: .NET Background Worker with persistent job table pattern.

Rationale:
- Reduced operational complexity compared to external message brokers
- Guaranteed delivery via database persistence
- Crash-safe retry mechanism
- Suitable for current system scale
- Message broker can be introduced later if required

Async processing uses a persistent background_jobs table
with polling workers using SELECT ... FOR UPDATE SKIP LOCKED.

This guarantees crash-safe, idempotent, retryable execution.

Rationale: - Lower operational overhead - Suitable for current traffic
expectations - Future queue integration possible if scaling required

## 6. Frontend

Chosen: Next.js (TypeScript)

Reasons: - SSR for SEO - Strong i18n support - Production maturity

## 7. Deployment

**Chosen:** Dokploy (Self-hosted PaaS) on VPS

**Previous Consideration:** Docker + VPS + Nginx (manuel yapılandırma)

### Architecture:

- **Dokploy** (PaaS yönetimi, Traefik entegre)
- **Traefik** (otomatik reverse proxy, SSL/TLS)
- API container (ASP.NET Core 10.0.3)
- Background Worker container
- Single Next.js container (public + admin)
- Redis container
- PostgreSQL container

### Rationale:

- **Reduced operational complexity:** Otomatik reverse proxy ve SSL yönetimi
- **Git-based deployment:** `git push` ile otomatik deploy
- **Automatic SSL/TLS:** Let's Encrypt entegrasyonu (Certbot gerektirmez)
- **Health check routing:** Traefik health check'lere göre traffic yönlendirme
- **Cost efficiency:** Tek VPS üzerinde tüm servisler
- **Self-hosted:** Veri kontrolü ve gizlilik (SaaS PaaS'e göre)
- **Dokku/Heroku-like experience:** Basit deployment deneyimi

### Trade-offs:

- ~~Nginx konfigürasyon kontrolü~~ → Traefik middleware yapılandırması
- ~~Manuel SSL sertifika yönetimi~~ → Otomatik Let's Encrypt (Traefik)
- ~~Blue/green deployment~~ → Dokploy native rollback (versiyon geçmişi)

# 8. Frontend Application Structure

## Context

The system requires:

- SEO-optimized public website
- Internal operational admin panel
- Minimal infrastructure complexity
- Resource efficiency on a single VPS (8GB RAM)
- Future scalability without premature separation

*Admin panel is for internal operational usage only and not public-facing.*

# Decision

A single Next.js application will serve both:

- `domain.com` → Public website
- `admin.domain.com` → Admin panel

Separation will be handled via:

- Hostname-based routing logic (middleware)
- Separate layout boundaries
- Backend RBAC enforcement

Deployment will use:

- Single build artifact
- Single frontend container
- Nginx host-based routing to same container


# Rationale

- Reduces DevOps complexity
- Reduces memory usage on VPS
- Simplifies CI/CD pipeline
- Avoids duplicate dependency management
- Maintains architectural clarity
- Allows future extraction if required

# Security Considerations

- Admin routes protected by middleware
- Backend enforces RBAC
- Admin APIs namespaced under `/api/admin/*`
- Admin not indexed by search engines
- JWT authentication required

# Future Evolution

If admin traffic or operational complexity increases:

- Admin can be extracted into a separate Next.js application
- Backend remains unchanged (API-first design)


## Technology Versions

| Component | Version | LTS/Status | Justification |
|-----------|---------|------------|---------------|
| .NET SDK | 10.0.103 | Stable SDK | Locked SDK feature band for local dev and CI |
| ASP.NET Core | 10.0.3 | LTS (Nov 2028) | Strong transaction support, mature ecosystem |
| Entity Framework Core | 10.0.0 | LTS | Native .NET 10 support, performance improvements |
| PostgreSQL | 18.3 | Stable | JSONB support, advanced indexing, ACID compliance |
| Redis | 7.4.x | Stable | Better clustering, improved ACL, hold TTL support |
| Next.js | 16.1.6 | Stable | App Router, Server Actions, SSR for SEO |
| React | 19.2.0 | Stable | Concurrent features, automatic batching |
| TypeScript | 5.3+ | Active | Strict typing, better IDE support |
| Tailwind CSS | 3.4+ | Active | Utility-first, rapid UI development |
| next-intl | 3.5+ | Active | i18n routing, RTL support |
| Node.js | 25.6.1 | Stable | Active LTS, stable for production |
| Docker | 29.2.1 | Current | BuildKit, improved security |
| Docker Compose | 2.20+ | Current | Compose spec v3, better networking |
| Dokploy | Latest | Active | Self-hosted PaaS, Git-based deployment |
| Traefik | Latest (Dokploy) | Stable | Auto SSL, reverse proxy, health-based routing |
| Ubuntu | 22.04 LTS | LTS (Apr 2027) | Server-grade stability, security updates |

## 9. i18n & Localization

### Decision
Multi-language support with 5 languages:
- Turkish (TR) - Default
- English (EN)
- Russian (RU)
- Arabic (AR) - RTL support required
- German (DE)

### Technology
- **next-intl**: i18n routing and message management
- **Locale path routing**: `/tr/`, `/en/`, `/ru/`, `/ar/`, `/de/`
- **RTL support**: Conditional CSS direction for Arabic
- **Admin panel**: Single language (Turkish only)

## 10. URL Structure & Routing

### Public Website
```
domain.com/tr/     → Turkish (default redirect from /)
domain.com/en/     → English
domain.com/ru/     → Russian
domain.com/ar/     → Arabic (RTL)
domain.com/de/     → German
domain.com/api/v1/ → API endpoints (language-agnostic)
```

### Admin Panel
```
admin.domain.com/       → Admin login/dashboard
admin.domain.com/api/   → Admin API endpoints
```

### Routing Logic
- **Traefik (Dokploy)**: Host-based routing (`domain.com` vs `admin.domain.com`), auto SSL
- **Next.js Middleware**: Locale detection and redirect
- **Backend**: JWT validation, RBAC enforcement

> **Not:** Nginx yerine Traefik kullanılmaktadır. Traefik, Dokploy ile birlikte gelen otomatik reverse proxy'dir ve Let's Encrypt SSL sertifikalarını otomatik yönetir.

## 11. Redis Resilience Strategy

### Decision
Redis is NOT the single source of truth. Degraded mode supported.

### Normal Mode
- Reservation holds stored in Redis (15-min TTL)
- High performance, automatic expiration

### Degraded Mode (Redis Down)
- Fallback to `reservation_holds` table in PostgreSQL
- `expires_at` field for manual cleanup
- Performance penalty accepted temporarily
- Booking continues without interruption
- Alert sent to monitoring

### Implementation
```sql
-- reservation_holds table (fallback)
CREATE TABLE reservation_holds (
    id UUID PRIMARY KEY,
    reservation_id UUID REFERENCES reservations(id),
    vehicle_id UUID REFERENCES vehicles(id),
    expires_at TIMESTAMP NOT NULL,
    status VARCHAR(20) DEFAULT 'active',
    created_at TIMESTAMP DEFAULT NOW()
);
CREATE INDEX idx_holds_expires ON reservation_holds(expires_at);
```

Backend: .NET SDK 10.0.103 / ASP.NET Core 10.0.3 (LTS)
Database: PostgreSQL 18.3
Cache: Redis 7.4.x
Frontend: Next.js 16.1.6 (App Router) + React 19.2.0
Runtime: Node 25.6.1
Container: Docker 29.2.1
PaaS: Dokploy (self-hosted) with Traefik
OS: Ubuntu 22.04 LTS

## 12. Testing Architecture Decisions

### 12.1 Integration Test Strategy

**Context:** Production launch öncesi kritik path'lerin gerçek bağımlılıklar (DB, Redis, API) ile test edilmesi gerekiyordu. Mevcut `RentACar.Tests` projesi EF InMemory kullanıyordu; bu integration test için yetersiz.

**Decision:** Ayrı bir `RentACar.ApiIntegrationTests` projesi oluşturuldu; `Microsoft.NET.Sdk.Web` + `WebApplicationFactory` kullanarak gerçek API pipeline'ını boot eder.

**Rationale:**
- WebApplicationFactory, gerçek middleware pipeline'ını, DI container'ını ve routing'i test eder.
- PostgreSQL ve Redis'e karşı gerçek integration sağlar; InMemory davranış farklılıklarından kaçınılır.
- CI'da ayrı job olarak çalıştırılabilir; servis bağımlılıkları explicit olarak tanımlanır.

**Trade-offs:**
- (+) Gerçekçi test ortamı; production'a daha yakın.
- (+) MockPaymentProvider'ın string trigger'ları ile failure injection kolaylaşır.
- (-) Daha yavaş çalışma süresi (DB/Redis bağlantıları, migration'lar).
- (-) CI ortamında PostgreSQL + Redis servisleri gereklidir.

**Consequences:**
- `backend/tests/RentACar.ApiIntegrationTests/` projesi eklendi.
- `Program.cs`'e `public partial class Program;` eklendi (WAF uyumu).
- CI workflow'u `backend-unit` ve `backend-integration` job'larına ayrıldı.
- Integration test'ler lokalde `docker compose up` gerektirir.

### 12.2 Test Isolation Pattern

**Decision:** Her integration test öncesi `PostgresFixture` yeni bir DB oluşturur; `RedisFixture` key prefix ile izolasyon sağlar.

**Rationale:**
- Testler arası veri kirliliğini önler.
- Paralel çalıştırma güvenli hale gelir.
- `DatabaseReset.TruncateAllAsync` ile test sonrası cleanup yapılır.

### 12.3 Mock Provider in Integration Tests

**Decision:** Integration test'lerde custom fake yerine gerçek `MockPaymentProvider` kullanılır.

**Rationale:**
- MockPaymentProvider zaten DI'da kayıtlı ve string trigger'ları var.
- Timeout, failure, 3DS decline senaryoları string trigger'ları ile test edilebilir.
- Ayrı fake implementasyonu maintain etmeye gerek kalmaz.

**Trigger Reference:**
| Trigger | Method | Effect |
|---------|--------|--------|
| `timeout` (in key/name) | `CreatePaymentIntentAsync` | Throws `TimeoutException` |
| `fail`/`cancel` (in bank response) | `VerifyPaymentAsync` | Returns `Failed` status |
| `fail` (in reason) | `RefundAsync` | Returns failure result |
| `fail` (in intent id) | `ReleaseDepositAsync` / `CaptureDepositAsync` | Returns failure result |
| `Amount <= 0` | `RefundAsync` / `CaptureDepositAsync` | Returns failure result |

### 12.4 Frontend Coverage Expansion Strategy

**Context:** Phase 10.1 backend-side coverage gates are now GO, and the 17 May 2026 frontend completion slice lifted overall frontend coverage from the earlier **28.41%** follow-up to **63.17%** with **190/190 PASS**. Public-facing pages already had strong file-level coverage; the closing slice focused on broader admin/dashboard pages, shared UI primitives, and UI hooks.

**Decision:** Keep frontend coverage expansion centered on Vitest + Testing Library tests that target real contracts. Admin/dashboard pages may mock `@/hooks/admin` data hooks and external UI side effects, while shared UI primitive tests should render real component APIs. Coverage collection excludes non-launch/test-support scaffold surfaces already outside the unit-test execution target: `e2e/**`, unused Tiptap editor scaffold, and `components/ui/kanban.tsx`.

**Rationale:**
- Keeps admin page tests deterministic without real backend or SWR/network dependencies.
- Preserves the existing Next.js App Router test pattern used by public route tests.
- Avoids brittle Radix/shadcn portal behavior by replacing complex primitives only where they block page-level behavior checks.
- Closed the frontend coverage gate through broad, previously uncovered admin/shared surfaces rather than over-farming already-covered public pages.

**Current Evidence (17 May 2026):**
- Frontend Vitest: **190/190 PASS**
- Frontend overall coverage: **63.17%**
- Validator-backed PR follow-through handoff: `docs/handoffs/2026-05-17-162725-phase10-frontend-coverage-pr-handoff.md`
- `frontend/app/(admin)/dashboard/(auth)/reservations/page.tsx`: **97.42% statements / 75.55% branches**
- `frontend/app/(admin)/dashboard/(auth)/reservations/[id]/page.tsx`: **97.37% statements / 72.09% branches**
- `frontend/hooks/admin`: **97.23% statements / 84.15% branches**
- `frontend/lib/api/admin/mock.ts`: **100% statements / branches / functions / lines**
- `frontend/lib/api/admin`: **72.84% statements / 57.59% branches**
- `frontend/lib/auth`: **63.43% statements / 85% branches**
- Fresh completion slice: `frontend/components/ui` **83.52%**, `frontend/hooks` **92.16%**, admin fleet/pricing/report page surfaces mostly **85–97%**.

**Consequences:**
- New admin page tests should use row-scoped Testing Library queries for icon-only actions.
- Complex shadcn/Radix primitives may be mocked at the component boundary when the test target is a page workflow rather than the primitive itself.
- API and auth helper tests may mock `../client` or `fetch`, but should assert endpoint construction, payload shape, and error/fallback branches rather than only importing modules for coverage.
- Phase 10.1 coverage gates are GO; further frontend work should prioritize meaningful auth route/screen and admin dialog behavior coverage instead of raw percentage gains.

### 12.5 Load Testing Validation Strategy

**Context:** Phase 10.4 load validation is being executed in the local Docker stack before any Dokploy rerun. This avoids coupling smoke verification to deployment readiness and lets the team exercise booking/payment/traffic scenarios against the same compose-backed runtime used in development.

**Decision:** Treat local Docker as the first validation environment for Phase 10.4 k6 runs, and defer Dokploy reruns until deployment infrastructure is actually available.

**Rationale:**
- Keeps load-validation work unblocked while Dokploy remains deferred.
- Lets smoke-mode test tuning happen against reproducible local containers.
- Preserves the distinction between local smoke evidence and deployed-infra evidence.

**Consequences:**
- `backend/tests/k6/` scripts should document any smoke-only assumptions, such as reduced VUs or feature-flag prerequisites.
- The launch-gate docs must explicitly distinguish local smoke partials from full load-baseline completion.
- Docker-local k6 runs that target the host backend may need an explicit `Host` header that matches `AllowedHosts`, and admin-dashboard smoke validation may require a seeded local admin account.
- As of 18 May 2026, the local Docker 100-user concurrent-booking baseline is also verified after local startup inventory seed expansion and overlap-retry stabilization in the reservation hold path.

### 12.6 Admin Reports Backend Scope

**Context:** Phase 10 Wave 4 required closure for admin reports and dashboard-only gaps. Settings/system persistence and maintenance completion remain launch-non-critical stubs, but the reports backend has a bounded read-only scope that can ship independently.

**Decision:** Implement admin reports as `AdminReportsController` + `IReportsService`/`ReportsService` under `/api/admin/v1/reports/*`, protected by the existing `AdminOnly` policy and standard rate-limit policy. Keep the launch scope to revenue, occupancy, and popular-vehicles reports using existing reservations, payments, and fleet tables.

**Rationale:**
- Keeps operational reports inside the existing monolith and admin API boundary.
- Avoids new persistence or projection infrastructure before launch.
- Preserves a clear deferred boundary for settings persistence and maintenance completion, which require separate configuration/migration decisions.

**Consequences:**
- The reports API contract is now tracked in `docs/07_API_Contract_ENTERPRISE_FULL.md`.
- Richer analytics, export workflows, and accounting-grade ledgers remain post-launch scope.
- Frontend report screens should consume these endpoints through the existing admin API client/hook patterns when that integration is scheduled.
