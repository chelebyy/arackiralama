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
