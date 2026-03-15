# Decisions

<!-- Append-only register of architectural and pattern decisions -->

| ID | Decision | Rationale | Date |
|----|----------|-----------|------|
| D001 | Modular Monolith | Microservice-ready, low operational complexity | 2026-03-14 |
| D002 | PostgreSQL + Redis | ACID + Cache combination | 2026-03-14 |
| D003 | Iyzico payment | Local provider for TR market | 2026-03-14 |
| D004 | Principal entities auto-normalize email on assignment | Keeps `Email`/`NormalizedEmail` invariant at the domain boundary and reduces call-site drift before query/index hardening tasks | 2026-03-15 |
| D005 | Auth persistence uses shared principal-discriminated token records (`AuthSession`, `PasswordResetToken`) with hash-at-rest fields only | Keeps admin/customer principals separate while enabling shared revocation/reset flows and enforces token-at-rest redaction at the schema level | 2026-03-15 |
| D006 | EF auth persistence maps `AuthPrincipalType` as string and enforces unique `normalized_email` indexes for both principals | Keeps schema diagnostics human-readable (`principal_type`) while hardening case-insensitive identity uniqueness at the database layer | 2026-03-15 |
| D007 | Auth foundation migration backfills normalized emails and fails fast on duplicate normalized principals before unique-index enforcement | Makes rollout safe for guest-created customer history by surfacing deterministic precondition errors instead of silently writing invalid defaults or failing late at index creation | 2026-03-15 |
