# Vehicle Rental Platform -- Enterprise Product Requirements Document (PRD)

Date: February 2026 Status: Enterprise Baseline Target Market: Turkey
(Initial: Alanya)

------------------------------------------------------------------------

# 1. Executive Summary

This document defines the full enterprise-grade requirements for a
multi-language vehicle rental platform.

The system must support: - Real-time availability - 15-minute
reservation hold - Online payment via bank POS abstraction - Deposit
pre-authorization - Admin operational control - Multi-language public
interface - Android-ready API architecture

Architecture decisions:
- Backend: .NET 10.0.3 (LTS)
- Database: PostgreSQL 18.3
- Cache: Redis 7.4.x (with DB fallback)
- Async Processing: .NET Background Worker
- Web: Next.js 16.1.6 (App Router)
- Admin: Next.js 16.1.6 (subdomain)
- i18n: 5 languages (TR/EN/RU/AR/DE)
- Deploy: Docker 29.2.1 + VPS
- Security: JWT + RBAC
- Payment: Provider abstraction (Halkbank ready)

------------------------------------------------------------------------
Cache: Redis Async Processing: .NET Background Worker
Web: Next.js - Admin: Next.js - Deploy: Docker + VPS - Security: JWT +
RBAC - Payment: Provider abstraction

------------------------------------------------------------------------

# 2. Product Scope

## 2.1 In Scope

-   Public vehicle search & booking
-   Online payment with 3D Secure
-   Deposit pre-authorization
-   Reservation lifecycle management
-   Admin vehicle & pricing management
-   Seasonal pricing rules
-   Campaign discounts
-   Airport delivery fee
-   Audit logging
-   Feature flag management

## 2.2 Out of Scope (Initial Release)

-   Multi-branch franchise management
-   Corporate invoicing automation
-   Dynamic pricing AI engine
-   Loyalty program

------------------------------------------------------------------------

# 3. User Types

## 3.1 Guest User

-   Can search vehicles
-   Can create reservation
-   Can complete payment
-   Can track reservation via public code

## 3.2 Registered User (Optional)

-   Save personal details
-   View reservation history

## 3.3 Admin

-   Manage vehicles
-   Manage reservations
-   Issue refunds
-   Release deposits
-   Manage pricing rules

## 3.4 Super Admin

-   Manage feature flags
-   Manage admin users
-   Access audit logs

------------------------------------------------------------------------

# 4. Functional Requirements

## FR-01 Vehicle Availability

System must: - Validate pickup \< return datetime - Prevent overlapping
reservations - Exclude vehicles in maintenance - Respect hold
reservations - Return available vehicle groups

Acceptance Criteria: - Two users cannot book the same vehicle for
overlapping times. - Hold blocks availability for 15 minutes.

------------------------------------------------------------------------

## FR-02 Reservation Creation

-   User selects pickup/return office and datetime
-   Vehicle group selection
-   Personal info validation
-   Reservation created in Draft status

State Flow: Draft → Hold → PendingPayment → Paid → Active → Completed
Failure: Cancelled / Expired / Failed

------------------------------------------------------------------------

## FR-03 Reservation Hold

-   15-minute TTL
-   Stored in Redis with expiration
-   Prevent double booking
-   Expired hold automatically releases vehicle

------------------------------------------------------------------------

## FR-04 Pricing Engine

Must support: - Seasonal pricing - Minimum rental days - Campaign code
discount - Airport delivery fee - Deposit calculation per vehicle group

Priority Order: Campaign → Seasonal → Base Price

------------------------------------------------------------------------

## FR-05 Payment Processing

-   Provider abstraction layer (IPaymentProvider)
-   3D Secure redirect flow
-   Idempotency key per payment attempt
-   Webhook verification (signature required)
-   Deposit pre-authorization

Edge Cases: - Payment success but response lost - Webhook delayed -
Duplicate webhook event

All must be idempotent.

------------------------------------------------------------------------

## FR-06 Admin Panel

Admin must be able to: - Assign vehicle manually - Cancel reservation -
Refund payment - Release deposit - Disable vehicle - Toggle feature
flags

All critical actions must create audit log entries.

------------------------------------------------------------------------

# 5. Non-Functional Requirements

## 5.1 Performance

-   API average response time \< 300ms
-   Availability query optimized via indexing
-   Redis used for hold + rate limiting

## 5.2 Availability

-   99% uptime target
-   Daily DB backup
-   Manual restore test monthly

## 5.3 Security

-   HTTPS only
-   JWT authentication
-   Role-based authorization (RBAC)
-   Payment webhook signature validation
-   No card data stored
-   PII masked in logs

## 5.4 Concurrency

-   Reservation overlap prevention at DB level
-   Transactional updates
-   Unique idempotency enforcement

------------------------------------------------------------------------

# 6. Reservation Business Rules

-   Minimum driver age per vehicle group
-   Minimum license years per vehicle group
-   Max hold per session: configurable
-   Late return penalty: configurable
-   Cancellation penalty: configurable
-   No-show policy: configurable

------------------------------------------------------------------------

# 7. Data Retention Policy

-   Reservation records: 5 years
-   Payment logs: 5 years
-   Audit logs: 3 years
-   SMS logs: 1 year

------------------------------------------------------------------------

# 8. Monitoring & Observability

System must monitor: - API uptime - 5xx error rate - Payment failure
rate - Disk usage - Background job backlog

------------------------------------------------------------------------

# 9. Success Metrics

-   Payment success rate \> 95%
-   Booking completion rate tracked
-   System error rate \< 2%
-   Double booking incidents = 0

------------------------------------------------------------------------

# 10. Future Expansion Readiness

System must be capable of: - Splitting into microservices if needed -
Supporting Android app via API - Horizontal scaling via additional API
instances

------------------------------------------------------------------------

# 11. Acceptance Criteria (Release Ready)

Release can go live only if:

-   Reservation overlap fully tested
-   Payment idempotency validated
-   Webhook replay protection confirmed
-   Backup restore test completed
-   Admin critical actions audited
-   Security review checklist completed

------------------------------------------------------------------------

END OF DOCUMENT
