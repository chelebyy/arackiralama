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
| D008 | JWT issuance now uses a shared principal claim model (`JwtPrincipalClaims`) for admin/customer tokens with mandatory `sid` and `ver` claims and a 15-minute access-token lifetime | Establishes a single token contract for both principal types ahead of session validation, while preserving Admin/SuperAdmin role-policy compatibility and making session diagnostics claim-decodable | 2026-03-15 |
| D009 | Refresh tokens are issued as 64-byte CSPRNG Base64Url values with `sha256:`-prefixed hash-at-rest and constant-time verification helper methods on `IJwtTokenService` | Standardizes safe token storage/validation primitives for upcoming session rotation and replay detection flows without exposing plaintext token material in persistence or diagnostics | 2026-03-15 |
| D010 | JWT bearer auth now performs application-level session checks via `IAccessTokenSessionValidator` and returns a structured `ApiResponse<object>` on unauthorized challenges | Ensures signed tokens are accepted only when backing session + principal token version are still valid while preserving non-leaky unauthorized response contracts for clients/diagnostics | 2026-03-15 |
| D011 | Auth conventions are centralized via shared claim/role constants and refresh-token cookie settings/service, with refresh tokens delivered via HttpOnly secure-ready cookie on admin login | Eliminates drift in role/claim naming across JWT issuance/validation, codifies Guest/Customer/Admin/SuperAdmin policy boundaries, and standardizes frontend-safe refresh token transport without exposing token material in API response bodies or logs | 2026-03-15 |
