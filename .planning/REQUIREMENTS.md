# Requirements: Araç Kiralama Platformu

**Defined:** 2026-03-14
**Core Value:** Kullanıcıların 15 dakikalık rezervasyon tutma, 3D Secure ödeme ve çok dilli destek ile güvenilir araç kiralama deneyimi.

## v1 Requirements (Remaining)

### AUTH - Authentication & Authorization

- [ ] **AUTH-01**: User can register with email and password
- [ ] **AUTH-02**: User can login with email and password
- [ ] **AUTH-03**: User receives JWT token valid for 15 minutes
- [ ] **AUTH-04**: User can refresh token within 7 days
- [ ] **AUTH-05**: User can logout (token revocation)
- [ ] **AUTH-06**: User can reset password via email link
- [ ] **AUTH-07**: Admin can login with email and password
- [ ] **AUTH-08**: SuperAdmin can manage admin users
- [ ] **AUTH-09**: System enforces RBAC (Guest, Customer, Admin, SuperAdmin)
- [ ] **AUTH-10**: Account locks after 5 failed login attempts

### NOTF - Notifications

- [ ] **NOTF-01**: Customer receives SMS on reservation confirmation
- [ ] **NOTF-02**: Customer receives SMS reminder 24h before pickup
- [ ] **NOTF-03**: Customer receives email on booking confirmation
- [ ] **NOTF-04**: Customer receives email on payment receipt
- [ ] **NOTF-05**: Admin receives notification on new reservation
- [ ] **NOTF-06**: SMS templates support 5 languages (TR/EN/RU/AR/DE)
- [ ] **NOTF-07**: Email templates support 5 languages
- [ ] **NOTF-08**: Background jobs process notifications asynchronously

### I18N - Internationalization

- [ ] **I18N-01**: Public website accessible in Turkish (default)
- [ ] **I18N-02**: Public website accessible in English
- [ ] **I18N-03**: Public website accessible in Russian
- [ ] **I18N-04**: Public website accessible in Arabic (RTL)
- [ ] **I18N-05**: Public website accessible in German
- [ ] **I18N-06**: User can switch language via selector
- [ ] **I18N-07**: URL includes locale prefix (/tr/, /en/, etc.)
- [ ] **I18N-08**: Arabic layout displays right-to-left

### FRONT - Frontend Public

- [ ] **FRONT-01**: User can view home page with search form
- [ ] **FRONT-02**: User can search available vehicles by dates and office
- [ ] **FRONT-03**: User can view vehicle details with pricing
- [ ] **FRONT-04**: User can complete 4-step booking flow
- [ ] **FRONT-05**: User can track reservation by public code
- [ ] **FRONT-06**: All pages are mobile-responsive
- [ ] **FRONT-07**: Pages load in under 3 seconds
- [ ] **FRONT-08**: Lighthouse score > 90

### FADM - Frontend Admin

- [ ] **FADM-01**: Admin can view dashboard with key metrics
- [ ] **FADM-02**: Admin can manage reservations (list, view, cancel)
- [ ] **FADM-03**: Admin can assign vehicle to reservation
- [ ] **FADM-04**: Admin can process check-in/check-out
- [ ] **FADM-05**: SuperAdmin can manage feature flags
- [ ] **FADM-06**: SuperAdmin can view audit logs

### DEPLOY - Infrastructure

- [ ] **DEPLOY-01**: Site accessible via HTTPS only
- [ ] **DEPLOY-02**: SSL certificate auto-renews
- [ ] **DEPLOY-03**: Database backup runs daily
- [ ] **DEPLOY-04**: Backup restore tested monthly
- [ ] **DEPLOY-05**: Health check endpoints respond 200 OK
- [ ] **DEPLOY-06**: Rate limiting active on all endpoints
- [ ] **DEPLOY-07**: Error monitoring configured

### TEST - Testing

- [ ] **TEST-01**: Unit test coverage > 70%
- [ ] **TEST-02**: Integration tests for all API endpoints
- [ ] **TEST-03**: E2E test for complete booking flow
- [ ] **TEST-04**: Security scan passes (OWASP Top 10)
- [ ] **TEST-05**: Load test handles 100 concurrent users

## v2 Requirements (Deferred)

### Social Login
- **AUTH-11**: User can login with Google OAuth
- **AUTH-12**: User can login with Facebook OAuth

### Two-Factor Auth
- **AUTH-13**: User can enable 2FA via SMS
- **AUTH-14**: User can enable 2FA via TOTP

### Advanced Notifications
- **NOTF-09**: WhatsApp notifications
- **NOTF-10**: Push notifications (PWA)

### Advanced Features
- **FRONT-09**: Vehicle comparison feature
- **FRONT-10**: Live chat support widget

## Out of Scope

| Feature | Reason |
|---------|--------|
| Real-time chat | High complexity, support ticket sufficient |
| Video uploads | Storage costs, photos sufficient |
| Dynamic AI pricing | Fixed seasonal pricing adequate |
| Multi-branch | Single location (Alanya) focus |
| Corporate accounts | B2C focus for MVP |
| Mobile app | Web-first, API-ready for future |
| Loyalty program | Deferred to v2 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| AUTH-01 | Phase 6 | Pending |
| AUTH-02 | Phase 6 | Pending |
| AUTH-03 | Phase 6 | Pending |
| AUTH-04 | Phase 6 | Pending |
| AUTH-05 | Phase 6 | Pending |
| AUTH-06 | Phase 6 | Pending |
| AUTH-07 | Phase 6 | Pending |
| AUTH-08 | Phase 6 | Pending |
| AUTH-09 | Phase 6 | Pending |
| AUTH-10 | Phase 6 | Pending |
| NOTF-01 | Phase 7 | Pending |
| NOTF-02 | Phase 7 | Pending |
| NOTF-03 | Phase 7 | Pending |
| NOTF-04 | Phase 7 | Pending |
| NOTF-05 | Phase 7 | Pending |
| NOTF-06 | Phase 7 | Pending |
| NOTF-07 | Phase 7 | Pending |
| NOTF-08 | Phase 7 | Pending |
| I18N-01 | Phase 8 | Pending |
| I18N-02 | Phase 8 | Pending |
| I18N-03 | Phase 8 | Pending |
| I18N-04 | Phase 8 | Pending |
| I18N-05 | Phase 8 | Pending |
| I18N-06 | Phase 8 | Pending |
| I18N-07 | Phase 8 | Pending |
| I18N-08 | Phase 8 | Pending |
| FRONT-01 | Phase 8 | Pending |
| FRONT-02 | Phase 8 | Pending |
| FRONT-03 | Phase 8 | Pending |
| FRONT-04 | Phase 8 | Pending |
| FRONT-05 | Phase 8 | Pending |
| FRONT-06 | Phase 8 | Pending |
| FRONT-07 | Phase 8 | Pending |
| FRONT-08 | Phase 8 | Pending |
| FADM-01 | Phase 8 | Pending |
| FADM-02 | Phase 8 | Pending |
| FADM-03 | Phase 8 | Pending |
| FADM-04 | Phase 8 | Pending |
| FADM-05 | Phase 8 | Pending |
| FADM-06 | Phase 8 | Pending |
| DEPLOY-01 | Phase 9 | Pending |
| DEPLOY-02 | Phase 9 | Pending |
| DEPLOY-03 | Phase 9 | Pending |
| DEPLOY-04 | Phase 9 | Pending |
| DEPLOY-05 | Phase 9 | Pending |
| DEPLOY-06 | Phase 9 | Pending |
| DEPLOY-07 | Phase 9 | Pending |
| TEST-01 | Phase 10 | Pending |
| TEST-02 | Phase 10 | Pending |
| TEST-03 | Phase 10 | Pending |
| TEST-04 | Phase 10 | Pending |
| TEST-05 | Phase 10 | Pending |

**Coverage:**
- v1 requirements: 47 total
- Mapped to phases: 47
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-14*
*Last updated: 2026-03-14 after GSD initialization*
