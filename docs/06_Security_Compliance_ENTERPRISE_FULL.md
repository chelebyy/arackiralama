# Security & Compliance Document

Date: 2026-02-25

## 1. Network Security

-   Firewall restricted to 80/443
-   No public DB access
-   SSH key-only login

## 2. Application Security

-   JWT authentication
-   RBAC authorization
-   Rate limiting (login, payment)
-   Idempotency enforcement

## 3. Payment Security

-   No card storage
-   Webhook signature validation
-   Deposit pre-auth via provider

## 4. Data Protection

-   TLS enforced
-   PII masked in logs
-   5-year payment log retention

## 5. Monitoring

-   Uptime monitoring
-   5xx alerts
-   Disk usage alerts
