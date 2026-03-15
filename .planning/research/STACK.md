# Stack Research - Araç Kiralama (Phases 6-10)

**Research Date:** 2026-03-14
**Context:** Brownfield - 5 phases complete, 5 remaining

## Current Stack (Validated)

| Layer | Technology | Version | Status |
|-------|------------|---------|--------|
| Backend | .NET SDK / ASP.NET Core | 10.0.103 / 10.0.3 | ✓ In use |
| Database | PostgreSQL | 18.3 | ✓ In use |
| Cache | Redis | 7.4.x | ✓ In use |
| ORM | Entity Framework Core | 10.0.0 | ✓ In use |
| Frontend | Next.js + React | 16.1.6 / 19.2.0 | ✓ In use |
| UI | Tailwind CSS + shadcn/ui | 3.4+ / latest | ✓ In use |
| Container | Docker | 29.2.1 | ✓ In use |

## Stack for Remaining Phases

### Phase 6: User Management & Auth

| Component | Choice | Rationale |
|-----------|--------|-----------|
| JWT Library | `System.IdentityModel.Tokens.Jwt` | Built-in .NET 10 support |
| Password Hashing | `BCrypt.Net-Next` 4.0.3 | Already implemented |
| Token Storage | HTTP-only cookie + localStorage | XSS protection |
| Refresh Tokens | PostgreSQL table | Revocation support |

### Phase 7: Notifications

| Component | Choice | Rationale |
|-----------|--------|-----------|
| SMS Primary | Netgsm API | Turkey-focused, reliable |
| SMS Fallback | Twilio | International backup |
| Email | SMTP (Resend/SendGrid) | Simple, cost-effective |
| Templates | Razor Templates | .NET native |

### Phase 8: Frontend

| Component | Choice | Rationale |
|-----------|--------|-----------|
| i18n | next-intl 3.5+ | App Router support, RTL |
| State | Zustand | Already in use |
| Forms | React Hook Form + Zod | Already in use |
| Charts | Recharts | Already in use |

### Phase 9: Infrastructure

| Component | Choice | Rationale |
|-----------|--------|-----------|
| OS | Ubuntu 22.04 LTS | Server stability |
| Reverse Proxy | Nginx 1.28.2 | SSL, rate limiting |
| SSL | Let's Encrypt | Free, auto-renewal |
| Monitoring | UptimeRobot + Docker logs | MVP approach |

### Phase 10: Testing

| Component | Choice | Rationale |
|-----------|--------|-----------|
| Backend Unit | xUnit + FluentAssertions | Already in use |
| Backend Integration | WebApplicationFactory | .NET native |
| Frontend Unit | Vitest + Testing Library | Already in use |
| E2E | Playwright | Cross-browser |

## Confidence Levels

| Phase | Confidence | Notes |
|-------|------------|-------|
| Phase 6 | High | JWT/RBAC well-documented |
| Phase 7 | Medium | SMS provider reliability varies |
| Phase 8 | High | Next.js i18n mature |
| Phase 9 | High | Standard deployment |
| Phase 10 | High | Testing patterns established |

---
*Stack research: 2026-03-14*
