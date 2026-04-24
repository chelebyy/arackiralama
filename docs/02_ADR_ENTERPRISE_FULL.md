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