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
