# Research Summary - Araç Kiralama (Phases 6-10)

**Research Date:** 2026-03-14

## Key Findings

### Stack (Already Validated)
- ✓ Backend: .NET 10 + ASP.NET Core 10 + EF Core 10
- ✓ Frontend: Next.js 16 + React 19 + Tailwind
- ✓ Database: PostgreSQL 18 + Redis 7.4
- ✓ Auth: JWT (System.IdentityModel.Tokens.Jwt)
- ✓ Payment: Iyzico/Mock provider abstraction

### Table Stakes (Remaining)

| Phase | Must-Have Features |
|-------|-------------------|
| 6 - Auth | JWT + refresh, RBAC, password reset, session mgmt |
| 7 - Notifications | SMS (Netgsm), Email (SMTP), templates, background jobs |
| 8 - Frontend | 5 languages, RTL, booking flow, admin panel |
| 9 - Infrastructure | HTTPS, backups, monitoring, deployment |
| 10 - Testing | Unit tests, integration tests, E2E tests |

### Watch Out For

1. **JWT Security** - 256-bit secret minimum, 15min expiry
2. **SMS Costs** - Use Netgsm (Turkish provider), implement rate limiting
3. **RTL Layout** - Test Arabic early, conditional CSS
4. **SSL Setup** - Let's Encrypt with auto-renewal
5. **Backup Strategy** - Daily automated, test restore monthly

## Build Order

```
Phase 6 (Auth) → Phase 7 (Notifications) → Phase 8 (Frontend)
                                                    ↓
Phase 9 (Infrastructure) ←─────────────────────────┘
           ↓
Phase 10 (Testing)
```

## Effort Estimates

| Phase | Duration | Key Tasks |
|-------|----------|-----------|
| 6 - Auth | 1 week | JWT, RBAC, refresh tokens |
| 7 - Notifications | 1 week | SMS, Email, templates |
| 8 - Frontend | 2-3 weeks | i18n, RTL, booking flow |
| 9 - Infrastructure | 1 week | VPS, SSL, monitoring |
| 10 - Testing | 1 week | Unit, integration, E2E |

**Total Remaining:** ~6-7 weeks

---
*Research synthesized: 2026-03-14*

# Architecture Research - Araç Kiralama (Phases 6-10)

**Research Date:** 2026-03-14
**Context:** Brownfield - Extending existing Clean Architecture

## Current Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    RentACar.API                         │
│  Controllers → Middleware → Services → Repositories     │
└─────────────────────────────────────────────────────────┘
                           │
┌──────────────────────────┴──────────────────────────────┐
│                 RentACar.Core                           │
│  Entities │ Interfaces │ Enums │ ValueObjects          │
└─────────────────────────────────────────────────────────┘
                           │
┌──────────────────────────┴──────────────────────────────┐
│             RentACar.Infrastructure                     │
│  DbContext │ Migrations │ Repositories │ Security      │
└─────────────────────────────────────────────────────────┘
```

## Phase 6: Auth Architecture

### New Components
```
RentACar.Core/
├── Entities/
│   ├── User.cs (NEW)
│   ├── RefreshToken.cs (NEW)
│   └── Role.cs (NEW)
├── Interfaces/
│   ├── IAuthService.cs (NEW)
│   └── ITokenService.cs (NEW)

RentACar.Infrastructure/
├── Services/
│   ├── JwtTokenService.cs (EXISTS - extend)
│   └── AuthService.cs (NEW)
```

### Auth Flow
```
1. Login → Validate credentials
2. Generate JWT (15min) + Refresh token (7d)
3. Store refresh token in DB
4. Client stores JWT in httpOnly cookie
5. Token refresh endpoint
```

## Phase 7: Notifications Architecture

### New Components
```
RentACar.Core/
├── Interfaces/
│   ├── ISmsProvider.cs (NEW)
│   └── IEmailProvider.cs (NEW)

RentACar.Infrastructure/
├── Services/
│   ├── Notifications/
│   │   ├── NetgsmSmsProvider.cs (NEW)
│   │   ├── TwilioSmsProvider.cs (NEW)
│   │   └── SmtpEmailProvider.cs (NEW)

RentACar.Worker/
├── Jobs/
│   ├── SendSmsJob.cs (NEW)
│   └── SendEmailJob.cs (NEW)
```

### Notification Flow
```
1. Event triggers (reservation created, etc.)
2. Create background job
3. Worker picks up job
4. Provider sends message
5. Log result
```

## Phase 8: Frontend Architecture

### Route Structure
```
frontend/app/
├── [locale]/                    # Public pages (i18n)
│   ├── page.tsx                 # Home
│   ├── vehicles/
│   │   ├── page.tsx             # Search results
│   │   └── [id]/page.tsx        # Vehicle detail
│   ├── booking/
│   │   ├── step-1/page.tsx      # Dates & office
│   │   ├── step-2/page.tsx      # Vehicle selection
│   │   ├── step-3/page.tsx      # Customer info
│   │   └── step-4/page.tsx      # Payment
│   └── tracking/page.tsx        # Reservation tracking
└── (admin)/dashboard/           # Admin (Turkish only)
```

### i18n Setup
```
frontend/
├── messages/
│   ├── tr.json
│   ├── en.json
│   ├── ru.json
│   ├── ar.json
│   └── de.json
├── i18n/
│   ├── config.ts
│   └── middleware.ts
```

## Phase 9: Deployment Architecture

```
┌─────────────────────────────────────────────────────────┐
│                      Nginx                              │
│  SSL Termination │ Rate Limiting │ Reverse Proxy       │
└─────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│   Next.js       │ │   API           │ │   Worker        │
│   Container     │ │   Container     │ │   Container     │
└─────────────────┘ └─────────────────┘ └─────────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         ▼                 ▼                 ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│   PostgreSQL    │ │     Redis       │ │   Backups       │
│   Container     │ │   Container     │ │   Volume        │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

## Build Order (Dependencies)

```
1. Phase 6: Auth (no dependencies)
2. Phase 7: Notifications (uses Auth for audit)
3. Phase 8: Frontend (uses Auth + existing APIs)
4. Phase 9: Infrastructure (all features ready)
5. Phase 10: Testing (all features complete)
```

---
*Architecture research: 2026-03-14*

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

# Features Research - Araç Kiralama (Phases 6-10)

**Research Date:** 2026-03-14
**Context:** Brownfield - Adding features to existing platform

## Table Stakes (Must Have)

### User Management (Phase 6)
- JWT authentication with refresh tokens
- Role-based access control (Guest, Customer, Admin, SuperAdmin)
- Password reset via email
- Session management
- Account lockout after failed attempts

### Notifications (Phase 7)
- SMS: Reservation confirmation, pickup reminder, return reminder
- Email: Booking confirmation, payment receipt, cancellation notice
- Template-based messages (multi-language)
- Opt-out capability for marketing

### Frontend (Phase 8)
- 5 language support (TR, EN, RU, AR, DE)
- RTL layout for Arabic
- Mobile-responsive design
- Search engine optimization
- Booking flow (4 steps)

### Infrastructure (Phase 9)
- HTTPS everywhere
- Daily database backups
- Health check endpoints
- Error monitoring
- Rate limiting

### Testing (Phase 10)
- Unit tests (>70% coverage)
- Integration tests
- E2E booking flow test
- Security scan

## Differentiators (Competitive Advantage)

### User Management
- Social login (Google) - Deferred to v2
- Two-factor authentication - Deferred to v2
- Driver license verification - Deferred to v2

### Notifications
- WhatsApp notifications - Deferred to v2
- Push notifications (PWA) - Deferred to v2

### Frontend
- Vehicle comparison feature - Deferred to v2
- Live chat support - Deferred to v2

## Anti-Features (Deliberately Excluded)

| Feature | Reason |
|---------|--------|
| Real-time chat | High complexity, support ticket sufficient |
| Video uploads | Storage costs, photos sufficient |
| Dynamic AI pricing | Fixed seasonal pricing adequate |
| Multi-branch | Single location (Alanya) focus |
| Corporate accounts | B2C focus for MVP |

## Feature Dependencies

```
Phase 6 (Auth) ─┬─> Phase 7 (Notifications) ─┬─> Phase 10 (Testing)
                 │                              │
                 └─> Phase 8 (Frontend) ────────┘
                                                │
Phase 9 (Infrastructure) ───────────────────────┘
```

## Complexity Estimates

| Feature | Complexity | Effort |
|---------|------------|--------|
| JWT Auth | Medium | 3-4 days |
| RBAC | Medium | 2-3 days |
| SMS Integration | Low | 1-2 days |
| Email Templates | Low | 1-2 days |
| i18n (5 languages) | Medium | 3-4 days |
| RTL Support | Medium | 2-3 days |
| VPS Deployment | Medium | 2-3 days |
| SSL Setup | Low | 0.5 day |
| E2E Tests | Medium | 2-3 days |

---
*Features research: 2026-03-14*

# Pitfalls Research - Araç Kiralama (Phases 6-10)

**Research Date:** 2026-03-14
**Context:** Brownfield - Avoiding common mistakes

## Phase 6: Auth Pitfalls

### JWT Security
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Short secrets | JWT secret < 32 chars | Use 256-bit random secret |
| No expiration | Tokens valid forever | 15min access, 7d refresh |
| Algorithm confusion | Accepting 'none' alg | Explicitly validate HS256 |
| Token in localStorage | XSS vulnerability | httpOnly cookies |

### RBAC Mistakes
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Client-side only auth | Frontend hides elements | Backend enforces all policies |
| Role escalation | User modifies token | Validate role from DB on sensitive ops |
| Missing audit | No log of access attempts | Log all auth failures |

**Phase to address:** Phase 6

## Phase 7: Notification Pitfalls

### SMS Provider Issues
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Single provider | Outage blocks all SMS | Fallback provider (Twilio) |
| No rate limiting | SMS spam | Queue with rate limits |
| Encoding issues | Turkish chars broken | Use UTF-8 encoding |
| Cost overrun | Unexpected bills | Daily limits, monitoring |

### Email Deliverability
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Spam folder | Low open rates | SPF, DKIM, DMARC records |
| No tracking | Unknown delivery | Webhook for delivery status |
| Template breaks | Missing variables | Validate all placeholders |

**Phase to address:** Phase 7

## Phase 8: Frontend Pitfalls

### i18n Mistakes
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Hardcoded strings | Text not translating | Extract all strings |
| RTL layout breaks | Arabic looks wrong | Test with RTL early |
| Date/number format | Inconsistent display | Use Intl API |
| Missing translations | Keys showing | Fallback language |

### SEO Issues
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| No meta tags | Poor search ranking | Dynamic meta per page |
| Slow initial load | High bounce rate | Server components |
| Missing sitemap | Pages not indexed | Generate sitemap.xml |

**Phase to address:** Phase 8

## Phase 9: Deployment Pitfalls

### Security Mistakes
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Default ports | 5432 exposed | Internal network only |
| No firewall | All ports open | UFW: only 80, 443, 22 |
| Missing backups | Data loss risk | Daily automated backups |
| No SSL | HTTP access | Force HTTPS redirect |

### Performance Issues
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| No monitoring | Silent failures | Health checks + alerts |
| Log rotation | Disk full | Configure log rotation |
| Memory leaks | OOM crashes | Container limits |

**Phase to address:** Phase 9

## Phase 10: Testing Pitfalls

### Test Coverage Mistakes
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Testing only happy path | Edge cases fail | Test error scenarios |
| Mock everything | Integration bugs | Integration tests |
| No E2E | User flow breaks | Playwright booking test |
| Flaky tests | Random failures | Fix or quarantine |

**Phase to address:** Phase 10

## Cross-Phase Pitfalls

### Database
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| No migrations in prod | Schema mismatch | Automated migration on deploy |
| Missing indexes | Slow queries | Add indexes for new tables |
| Connection exhaustion | Timeouts | Pool sizing, connection string |

### API
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Breaking changes | Client errors | Version API (v1, v2) |
| No rate limiting | DDoS vulnerable | Already implemented ✓ |
| Missing validation | Invalid data | FluentValidation on all inputs |

---
*Pitfalls research: 2026-03-14*