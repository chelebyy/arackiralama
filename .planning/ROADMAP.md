# Roadmap: Araç Kiralama Platformu

**Created:** 2026-03-14
**Status:** Active Development
**Total Phases:** 5 (Phases 6-10, extending existing system)

---

## Phase 6: User Management & Auth

**Goal:** Implement JWT-based authentication with RBAC for customers and admins

**Duration:** 1-2 weeks

### Requirements Covered
- AUTH-01: User registration
- AUTH-02: User login
- AUTH-03: JWT token generation
- AUTH-04: Refresh token
- AUTH-05: Password reset
- AUTH-06: Role management
- AUTH-07: Session management
- AUTH-08: Account lockout

### Success Criteria
1. User can register and login successfully
2. JWT tokens expire after 15 minutes
3. Refresh tokens valid for 7 days
4. Account locks after 5 failed attempts
5. Password reset email sent successfully

### Plans
1. **User Entity & Repository** - Create User, RefreshToken entities and repositories
2. **JWT Service** - Implement token generation and validation
3. **Auth Controller** - Create login, register, refresh, logout endpoints
4. **RBAC Middleware** - Implement role-based authorization
5. **Password Reset Flow** - Email-based password reset

---

## Phase 7: Notifications & Background Jobs

**Goal:** Implement SMS and email notifications with background job processing

**Duration:** 1 week

### Requirements Covered
- NOTF-01: Booking confirmation SMS
- NOTF-02: Booking confirmation email
- NOTF-03: Payment receipt email
- NOTF-04: Admin notifications
- NOTF-05: SMS templates (5 languages)
- NOTF-06: Email templates (5 languages)
- NOTF-07: Background job processing
- NOTF-08: Async notification dispatch

### Success Criteria
1. SMS sent within 5 seconds of job creation
2. Email templates render correctly in all 5 languages
3. Background jobs retry on failure
4. Failed jobs logged with error details
5. Admin receives real-time notifications

### Plans
1. **SMS Provider Interface** - ISmsProvider with Netgsm implementation
2. **Email Provider Interface** - IEmailProvider with SMTP implementation
3. **Notification Templates** - Multi-language templates for SMS/Email
4. **Background Jobs** - SendSmsJob, SendEmailJob implementations
5. **Notification Service** - Coordinating SMS/Email dispatch

---

## Phase 8: Frontend (Public + Admin)

**Goal:** Build multi-language public website and extend admin panel

**Duration:** 2-3 weeks

### Requirements Covered
- I18N-01 to I18N-08: Multi-language support (TR/EN/RU/AR/DE)
- FRONT-01 to FRONT-08: Public website pages
- FADM-01 to FADM-06: Admin panel extensions

### Success Criteria
1. All 5 languages accessible via URL prefix
2. Arabic layout displays RTL correctly
3. Booking flow completes end-to-end
4. Lighthouse score > 90
5. Admin dashboard shows real-time metrics
6. SuperAdmin can manage feature flags

### Plans
1. **i18n Setup** - next-intl configuration with 5 locales
2. **Public Home Page** - Hero section, search form, featured vehicles
3. **Search Results Page** - Vehicle availability, filtering, pricing
4. **Booking Flow** - 4-step reservation process
5. **Reservation Tracking** - Public code lookup
6. **Admin Extensions** - Dashboard metrics, audit logs, feature flags

---

## Phase 9: Infrastructure & Deployment

**Goal:** Production deployment with SSL, monitoring, and backups

**Duration:** 1 week

### Requirements Covered
- DEPLOY-01: HTTPS enforcement
- DEPLOY-02: SSL auto-renewal
- DEPLOY-03: Daily backups
- DEPLOY-04: Backup restore testing
- DEPLOY-05: Health checks
- DEPLOY-06: Rate limiting
- DEPLOY-07: Error monitoring

### Success Criteria
1. Site accessible via HTTPS only
2. SSL certificate auto-renews
3. Backup runs daily at 2 AM
4. Restore tested successfully
5. Health endpoint returns 200 OK
6. Rate limiting prevents abuse
7. Errors logged to monitoring service

### Plans
1. **VPS Setup** - Ubuntu 22.04, Docker, firewall
2. **SSL Configuration** - Let's Encrypt with auto-renewal
3. **Backup Strategy** - Daily PostgreSQL backups, rotation
4. **Monitoring Setup** - Health checks, error logging
5. **Deployment Script** - Zero-downtime deployment

---

## Phase 10: Testing & Launch

**Goal:** Comprehensive testing and production launch

**Duration:** 1 week

### Requirements Covered
- TEST-01: Unit test coverage > 70%
- TEST-02: Integration tests
- TEST-03: E2E booking flow
- TEST-04: Security scan
- TEST-05: Load testing

### Success Criteria
1. Unit test coverage > 70%
2. All API endpoints have integration tests
3. E2E test completes full booking flow
4. Security scan passes (OWASP Top 10)
5. System handles 100 concurrent users

### Plans
1. **Unit Tests** - Service and repository tests
2. **Integration Tests** - API endpoint tests with test database
3. **E2E Tests** - Playwright booking flow test
4. **Security Tests** - OWASP ZAP scan
5. **Load Tests** - k6 load testing
6. **Launch Preparation** - DNS, final checks, go-live

---

## Progress Summary

| Phase | Status | Plans | Progress |
|-------|--------|-------|----------|
| 1-5 | ✓ Complete | - | 100% |
| 6 | ○ Pending | 5 | 0% |
| 7 | ○ Pending | 5 | 0% |
| 8 | ○ Pending | 6 | 0% |
| 9 | ○ Pending | 5 | 0% |
| 10 | ○ Pending | 6 | 0% |

**Total:** 5 phases remaining, 27 plans to execute

---
*Roadmap created: 2026-03-14*
