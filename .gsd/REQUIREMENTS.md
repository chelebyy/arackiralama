# Requirements

## Status Buckets (M001 complete verification)

### Active
- None

### Validated
- AUTH-01, AUTH-02, AUTH-03, AUTH-04, AUTH-05, AUTH-06, AUTH-07, AUTH-08, AUTH-09, AUTH-10
  - S04 verification proves password reset request/confirm lifecycle (non-enumeration, token hash-at-rest, single-use consume, token-version/session invalidation), admin auth lifecycle parity (normalized-email login, lockout, refresh rotation, logout revocation), and SuperAdmin-only management/RBAC policy closure.
  - S05 verification proves frontend integration closure: customer/admin login UX wired to real backend contracts via Next.js BFF handlers, forgot/reset password UX completion, proxy-level RBAC route gating (Guest/Customer/Admin/SuperAdmin), refresh continuity via cookie-backed rotation, and logout cleanup of frontend+backend session state.

### Deferred
- None

### Blocked
- None

### Out of Scope
- None

## v1: Core Platform

### AUTH: Authentication & Authorization
- [x] **AUTH-01**: User can register with email and password
- [x] **AUTH-02**: User can login with email and password
- [x] **AUTH-03**: User receives JWT token valid for 15 minutes
- [x] **AUTH-04**: User can refresh token within 7 days
- [x] **AUTH-05**: User can logout (token revocation)
- [x] **AUTH-06**: User can reset password via email link
- [x] **AUTH-07**: Admin can login with email and password
- [x] **AUTH-08**: SuperAdmin can manage admin users
- [x] **AUTH-09**: System enforces RBAC (Guest, Customer, Admin, SuperAdmin)
- [x] **AUTH-10**: Account locks after 5 failed login attempts

### NOTF: Notifications & Background Jobs
- [ ] **NOTF-01**: Customer receives SMS on reservation confirmation
- [ ] **NOTF-02**: Customer receives SMS reminder 24h before pickup
- [ ] **NOTF-03**: Customer receives email on booking confirmation
- [ ] **NOTF-04**: Customer receives email on payment receipt
- [ ] **NOTF-05**: Admin receives notification on new reservation
- [ ] **NOTF-06**: SMS templates support 5 languages (TR/EN/RU/AR/DE)
- [ ] **NOTF-07**: Email templates support 5 languages
- [ ] **NOTF-08**: Background jobs process notifications asynchronously

### I18N: Internationalization
- [ ] **I18N-01**: Public website accessible in Turkish (default)
- [ ] **I18N-02**: Public website accessible in English
- [ ] **I18N-03**: Public website accessible in Russian
- [ ] **I18N-04**: Public website accessible in Arabic (RTL)
- [ ] **I18N-05**: Public website accessible in German
- [ ] **I18N-06**: User can switch language via selector
- [ ] **I18N-07**: URL includes locale prefix (/tr/, /en/, etc.)
- [ ] **I18N-08**: Arabic layout displays right-to-left

### FRONT: Public Website
- [ ] **FRONT-01**: User can view home page with search form
- [ ] **FRONT-02**: User can search available vehicles by dates and office
- [ ] **FRONT-03**: User can view vehicle details with pricing
- [ ] **FRONT-04**: User can complete 4-step booking flow
- [ ] **FRONT-05**: User can track reservation by public code
- [ ] **FRONT-06**: All pages are mobile-responsive
- [ ] **FRONT-07**: Pages load in under 3 seconds
- [ ] **FRONT-08**: Lighthouse score > 90

### FADM: Admin Dashboard Extensions
- [ ] **FADM-01**: Admin can view dashboard with key metrics
- [ ] **FADM-02**: Admin can manage reservations (list, view, cancel)
- [ ] **FADM-03**: Admin can assign vehicle to reservation
- [ ] **FADM-04**: Admin can process check-in/check-out
- [ ] **FADM-05**: SuperAdmin can manage feature flags
- [ ] **FADM-06**: SuperAdmin can view audit logs

### DEPLOY: Infrastructure & Operations
- [ ] **DEPLOY-01**: Site accessible via HTTPS only
- [ ] **DEPLOY-02**: SSL certificate auto-renews
- [ ] **DEPLOY-03**: Database backup runs daily
- [ ] **DEPLOY-04**: Backup restore tested monthly
- [ ] **DEPLOY-05**: Health check endpoints respond 200 OK
- [ ] **DEPLOY-06**: Rate limiting active on all endpoints
- [ ] **DEPLOY-07**: Error monitoring configured

### TEST: Quality Assurance
- [ ] **TEST-01**: Unit test coverage > 70%
- [ ] **TEST-02**: Integration tests for all API endpoints
- [ ] **TEST-03**: E2E test for complete booking flow
- [ ] **TEST-04**: Security scan passes (OWASP Top 10)
- [ ] **TEST-05**: Load test handles 100 concurrent users

## v2: Future Roadmap
- [ ] **SOCIAL-01**: Google/Facebook OAuth
- [ ] **AUTH-2FA**: SMS/TOTP Two-Factor Authentication
- [ ] **NOTF-ADV**: WhatsApp and Push notifications
- [ ] **FEAT-COMP**: Vehicle comparison
- [ ] **FEAT-CHAT**: Live chat support
