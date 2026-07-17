# Architecture Decision Record (ADR)

Date: 2026-02-25 Status: Accepted
Updated: 2026-07-17 (public customer membership surface disabled)

## 1. Backend Technology

Chosen: .NET SDK 10.0.103 + ASP.NET Core 10.0.3 (LTS) Reason: - Long-term support - Strong transaction &
concurrency handling - Mature ecosystem - Suitable for financial
integrations

## 2. Architecture Style

Chosen: Modular Monolith (Microservice-ready)

Rationale: - Reduced operational complexity - Clear domain boundaries -
Future service extraction possible

## 3. Database

Chosen: PostgreSQL

Rationale: - ACID compliance - Strong row-level locking - Advanced
indexing - JSONB support - Suitable for reservation overlap control

## 4. Cache Layer

Chosen: Redis

Used for: - Reservation hold TTL - Rate limiting - Short-lived caching

## 5. Async Processing

Chosen: .NET Background Worker with persistent job table pattern.

Rationale:

- Reduced operational complexity compared to external message brokers
- Guaranteed delivery via database persistence
- Crash-safe retry mechanism
- Suitable for current system scale
- Message broker can be introduced later if required

Async processing uses a persistent background_jobs table
with polling workers using SELECT ... FOR UPDATE SKIP LOCKED.

This guarantees crash-safe, idempotent, retryable execution.

Rationale: - Lower operational overhead - Suitable for current traffic
expectations - Future queue integration possible if scaling required

## 6. Frontend

Chosen: Next.js (TypeScript)

Reasons: - SSR for SEO - Strong i18n support - Production maturity

## 7. Deployment

**Chosen:** Dokploy (Self-hosted PaaS) on VPS

**Previous Consideration:** Docker + VPS + Nginx (manuel yapılandırma)

### Architecture:

- **Dokploy** (PaaS yönetimi, Traefik entegre)
- **Traefik** (otomatik reverse proxy, SSL/TLS)
- API container (ASP.NET Core 10.0.3)
- Background Worker container
- Single Next.js container (public + admin)
- Redis container
- PostgreSQL container

### Rationale:

- **Reduced operational complexity:** Otomatik reverse proxy ve SSL yönetimi
- **Git-based deployment:** `git push` ile otomatik deploy
- **Automatic SSL/TLS:** Let's Encrypt entegrasyonu (Certbot gerektirmez)
- **Health check routing:** Traefik health check'lere göre traffic yönlendirme
- **Cost efficiency:** Tek VPS üzerinde tüm servisler
- **Self-hosted:** Veri kontrolü ve gizlilik (SaaS PaaS'e göre)
- **Dokku/Heroku-like experience:** Basit deployment deneyimi

### Trade-offs:

- ~~Nginx konfigürasyon kontrolü~~ → Traefik middleware yapılandırması
- ~~Manuel SSL sertifika yönetimi~~ → Otomatik Let's Encrypt (Traefik)
- ~~Blue/green deployment~~ → Dokploy native rollback (versiyon geçmişi)

# 8. Frontend Application Structure

## Context

The system requires:

- SEO-optimized public website
- Internal operational admin panel
- Minimal infrastructure complexity
- Resource efficiency on a single VPS (8GB RAM)
- Future scalability without premature separation

_Admin panel is for internal operational usage only and not public-facing._

# Decision

A single Next.js application will serve both:

- `domain.com` → Public website
- `admin.domain.com` → Admin panel

Separation will be handled via:

- Hostname-based routing logic (middleware)
- Separate layout boundaries
- Backend RBAC enforcement

Deployment will use:

- Single build artifact
- Single frontend container
- Nginx host-based routing to same container

# Rationale

- Reduces DevOps complexity
- Reduces memory usage on VPS
- Simplifies CI/CD pipeline
- Avoids duplicate dependency management
- Maintains architectural clarity
- Allows future extraction if required

# Security Considerations

- Admin routes protected by middleware
- Backend enforces RBAC
- Admin APIs namespaced under `/api/admin/*`
- Admin not indexed by search engines
- JWT authentication required

# Future Evolution

If admin traffic or operational complexity increases:

- Admin can be extracted into a separate Next.js application
- Backend remains unchanged (API-first design)

## Technology Versions

| Component             | Version          | LTS/Status     | Justification                                     |
| --------------------- | ---------------- | -------------- | ------------------------------------------------- |
| .NET SDK              | 10.0.103         | Stable SDK     | Locked SDK feature band for local dev and CI      |
| ASP.NET Core          | 10.0.3           | LTS (Nov 2028) | Strong transaction support, mature ecosystem      |
| Entity Framework Core | 10.0.0           | LTS            | Native .NET 10 support, performance improvements  |
| PostgreSQL            | 18.3             | Stable         | JSONB support, advanced indexing, ACID compliance |
| Redis                 | 7.4.x            | Stable         | Better clustering, improved ACL, hold TTL support |
| Next.js               | 16.1.6           | Stable         | App Router, Server Actions, SSR for SEO           |
| React                 | 19.2.0           | Stable         | Concurrent features, automatic batching           |
| TypeScript            | 5.3+             | Active         | Strict typing, better IDE support                 |
| Tailwind CSS          | 3.4+             | Active         | Utility-first, rapid UI development               |
| next-intl             | 3.5+             | Active         | i18n routing, RTL support                         |
| Node.js               | 25.6.1           | Stable         | Active LTS, stable for production                 |
| Docker                | 29.2.1           | Current        | BuildKit, improved security                       |
| Docker Compose        | 2.20+            | Current        | Compose spec v3, better networking                |
| Dokploy               | Latest           | Active         | Self-hosted PaaS, Git-based deployment            |
| Traefik               | Latest (Dokploy) | Stable         | Auto SSL, reverse proxy, health-based routing     |
| Ubuntu                | 22.04 LTS        | LTS (Apr 2027) | Server-grade stability, security updates          |

## 9. i18n & Localization

### Decision

Multi-language support with 5 languages:

- Turkish (TR) - Default
- English (EN)
- Russian (RU)
- Arabic (AR) - RTL support required
- German (DE)

### Technology

- **next-intl**: i18n routing and message management
- **Locale path routing**: `/tr/`, `/en/`, `/ru/`, `/ar/`, `/de/`
- **RTL support**: Conditional CSS direction for Arabic
- **Admin panel**: Single language (Turkish only)
- **Admin-managed public content**: Public legal/contact/navigation content is stored in `PublicSiteSettings` JSON sections with base fields as the fallback and optional `translations.{locale}.{field}` overrides for `tr`, `en`, `ru`, `ar`, and `de`. The admin UI remains Turkish, but public content editors expose five locale tabs for managed pages, header/footer links, hero CTA, and contact-page rows.
- **Admin settings boundary**: Operational/system settings and public-site content are separated in the dashboard. `/dashboard/settings/system` owns technical settings, while `/dashboard/settings/public-content` owns customer-facing managed content for `contact`/`iletisim`, `privacy`, `terms`, navigation links, contact rows, and map/payment public display settings. Public routes read through the unauthenticated public settings endpoint and keep message-file fallbacks for incomplete managed records.
- **Admin authoring UX boundary**: Public Site & Contact authoring must make locale-specific content, global contact settings, draft/published/hidden state, and save progress visible without changing the public route contract. The settings navigation may scroll within its own container on narrow mobile viewports, but it must not create page-level horizontal overflow. Docker Desktop browser evidence for this boundary is tracked in `docs/13_Local_Docker_Browser_Test_Checklist.md` and `docs/test-evidence/local-docker-2026-07-08-admin-ux/`.

## 10. URL Structure & Routing

### Public Website

```
domain.com/tr/     → Turkish (default redirect from /)
domain.com/en/     → English
domain.com/ru/     → Russian
domain.com/ar/     → Arabic (RTL)
domain.com/de/     → German
domain.com/api/v1/ → API endpoints (language-agnostic)
```

### Admin Panel

```
admin.domain.com/       → Admin login/dashboard
admin.domain.com/api/   → Admin API endpoints
```

### Routing Logic

- **Traefik (Dokploy)**: Host-based routing (`domain.com` vs `admin.domain.com`), auto SSL
- **Next.js Middleware**: Locale detection and redirect
- **Backend**: JWT validation, RBAC enforcement

> **Not:** Nginx yerine Traefik kullanılmaktadır. Traefik, Dokploy ile birlikte gelen otomatik reverse proxy'dir ve Let's Encrypt SSL sertifikalarını otomatik yönetir.

## 11. Redis Resilience Strategy

### Decision

Redis is NOT the single source of truth. Degraded mode supported.

### Normal Mode

- Reservation holds stored in Redis (15-min TTL)
- High performance, automatic expiration

### Degraded Mode (Redis Down)

- Fallback to `reservation_holds` table in PostgreSQL
- `expires_at` field for manual cleanup
- Performance penalty accepted temporarily
- Booking continues without interruption
- Alert sent to monitoring

### Implementation

```sql
-- reservation_holds table (fallback)
CREATE TABLE reservation_holds (
    id UUID PRIMARY KEY,
    reservation_id UUID REFERENCES reservations(id),
    vehicle_id UUID REFERENCES vehicles(id),
    expires_at TIMESTAMP NOT NULL,
    status VARCHAR(20) DEFAULT 'active',
    created_at TIMESTAMP DEFAULT NOW()
);
CREATE INDEX idx_holds_expires ON reservation_holds(expires_at);
```

Backend: .NET SDK 10.0.103 / ASP.NET Core 10.0.3 (LTS)
Database: PostgreSQL 18.3
Cache: Redis 7.4.x
Frontend: Next.js 16.1.6 (App Router) + React 19.2.0
Runtime: Node 25.6.1
Container: Docker 29.2.1
PaaS: Dokploy (self-hosted) with Traefik
OS: Ubuntu 22.04 LTS

## 12. Testing Architecture Decisions

### 12.1 Integration Test Strategy

**Context:** Production launch öncesi kritik path'lerin gerçek bağımlılıklar (DB, Redis, API) ile test edilmesi gerekiyordu. Mevcut `RentACar.Tests` projesi EF InMemory kullanıyordu; bu integration test için yetersiz.

**Decision:** Ayrı bir `RentACar.ApiIntegrationTests` projesi oluşturuldu; `Microsoft.NET.Sdk.Web` + `WebApplicationFactory` kullanarak gerçek API pipeline'ını boot eder.

**Rationale:**

- WebApplicationFactory, gerçek middleware pipeline'ını, DI container'ını ve routing'i test eder.
- PostgreSQL ve Redis'e karşı gerçek integration sağlar; InMemory davranış farklılıklarından kaçınılır.
- CI'da ayrı job olarak çalıştırılabilir; servis bağımlılıkları explicit olarak tanımlanır.

**Trade-offs:**

- (+) Gerçekçi test ortamı; production'a daha yakın.
- (+) MockPaymentProvider'ın string trigger'ları ile failure injection kolaylaşır.
- (-) Daha yavaş çalışma süresi (DB/Redis bağlantıları, migration'lar).
- (-) CI ortamında PostgreSQL + Redis servisleri gereklidir.

**Consequences:**

- `backend/tests/RentACar.ApiIntegrationTests/` projesi eklendi.
- `Program.cs`'e `public partial class Program;` eklendi (WAF uyumu).
- CI workflow'u `backend-unit` ve `backend-integration` job'larına ayrıldı.
- Integration test'ler lokalde `docker compose up` gerektirir.

### 12.2 Test Isolation Pattern

**Decision:** Her integration test öncesi `PostgresFixture` yeni bir DB oluşturur; `RedisFixture` key prefix ile izolasyon sağlar.

**Rationale:**

- Testler arası veri kirliliğini önler.
- Paralel çalıştırma güvenli hale gelir.
- `DatabaseReset.TruncateAllAsync` ile test sonrası cleanup yapılır.

### 12.3 Mock Provider in Integration Tests

**Decision:** Integration test'lerde custom fake yerine gerçek `MockPaymentProvider` kullanılır.

**Rationale:**

- MockPaymentProvider zaten DI'da kayıtlı ve string trigger'ları var.
- Timeout, failure, 3DS decline senaryoları string trigger'ları ile test edilebilir.
- Ayrı fake implementasyonu maintain etmeye gerek kalmaz.

**Trigger Reference:**
| Trigger | Method | Effect |
|---------|--------|--------|
| `timeout` (in key/name) | `CreatePaymentIntentAsync` | Throws `TimeoutException` |
| `fail`/`cancel` (in bank response) | `VerifyPaymentAsync` | Returns `Failed` status |
| `fail` (in reason) | `RefundAsync` | Returns failure result |
| `fail` (in intent id) | `ReleaseDepositAsync` / `CaptureDepositAsync` | Returns failure result |
| `Amount <= 0` | `RefundAsync` / `CaptureDepositAsync` | Returns failure result |

### 12.4 Frontend Coverage Expansion Strategy

**Context:** Phase 10.1 backend-side coverage gates are now GO, and the 17 May 2026 frontend completion slice lifted overall frontend coverage from the earlier **28.41%** follow-up to **63.17%** with **190/190 PASS**. Public-facing pages already had strong file-level coverage; the closing slice focused on broader admin/dashboard pages, shared UI primitives, and UI hooks.

**Decision:** Keep frontend coverage expansion centered on Vitest + Testing Library tests that target real contracts. Admin/dashboard pages may mock `@/hooks/admin` data hooks and external UI side effects, while shared UI primitive tests should render real component APIs. Coverage collection excludes non-launch/test-support scaffold surfaces already outside the unit-test execution target: `e2e/**`, unused Tiptap editor scaffold, and `components/ui/kanban.tsx`.

**Rationale:**

- Keeps admin page tests deterministic without real backend or SWR/network dependencies.
- Preserves the existing Next.js App Router test pattern used by public route tests.
- Avoids brittle Radix/shadcn portal behavior by replacing complex primitives only where they block page-level behavior checks.
- Closed the frontend coverage gate through broad, previously uncovered admin/shared surfaces rather than over-farming already-covered public pages.

**Current Evidence (17 May 2026):**

- Frontend Vitest: **190/190 PASS**
- Frontend overall coverage: **63.17%**
- Validator-backed PR follow-through handoff: `docs/handoffs/2026-05-17-162725-phase10-frontend-coverage-pr-handoff.md`
- `frontend/app/(admin)/dashboard/(auth)/reservations/page.tsx`: **97.42% statements / 75.55% branches**
- `frontend/app/(admin)/dashboard/(auth)/reservations/[id]/page.tsx`: **97.37% statements / 72.09% branches**
- `frontend/hooks/admin`: **97.23% statements / 84.15% branches**
- `frontend/lib/api/admin/mock.ts`: **100% statements / branches / functions / lines**
- `frontend/lib/api/admin`: **72.84% statements / 57.59% branches**
- `frontend/lib/auth`: **63.43% statements / 85% branches**
- Fresh completion slice: `frontend/components/ui` **83.52%**, `frontend/hooks` **92.16%**, admin fleet/pricing/report page surfaces mostly **85–97%**.

**Consequences:**

- New admin page tests should use row-scoped Testing Library queries for icon-only actions.
- Complex shadcn/Radix primitives may be mocked at the component boundary when the test target is a page workflow rather than the primitive itself.
- API and auth helper tests may mock `../client` or `fetch`, but should assert endpoint construction, payload shape, and error/fallback branches rather than only importing modules for coverage.
- Phase 10.1 coverage gates are GO; further frontend work should prioritize meaningful auth route/screen and admin dialog behavior coverage instead of raw percentage gains.

### 12.5 Load Testing Validation Strategy

**Context:** Phase 10.4 load validation is being executed in the local Docker stack before any Dokploy rerun. This avoids coupling smoke verification to deployment readiness and lets the team exercise booking/payment/traffic scenarios against the same compose-backed runtime used in development.

**Decision:** Treat local Docker as the first validation environment for Phase 10.4 k6 runs, and defer Dokploy reruns until deployment infrastructure is actually available.

**Rationale:**

- Keeps load-validation work unblocked while Dokploy remains deferred.
- Lets smoke-mode test tuning happen against reproducible local containers.
- Preserves the distinction between local smoke evidence and deployed-infra evidence.

**Consequences:**

- `backend/tests/k6/` scripts should document any smoke-only assumptions, such as reduced VUs or feature-flag prerequisites.
- The launch-gate docs must explicitly distinguish local smoke partials from full load-baseline completion.
- Docker-local k6 runs that target the host backend may need an explicit `Host` header that matches `AllowedHosts`, and admin-dashboard smoke validation may require a seeded local admin account.
- As of 18 May 2026, the local Docker 100-user concurrent-booking baseline is also verified after local startup inventory seed expansion and overlap-retry stabilization in the reservation hold path.

### 12.6 Admin Reports Backend Scope

**Context:** Phase 10 Wave 4 required closure for admin reports and dashboard-only gaps. Settings/system persistence and maintenance completion remain launch-non-critical stubs, but the reports backend has a bounded read-only scope that can ship independently.

**Decision:** Implement admin reports as `AdminReportsController` + `IReportsService`/`ReportsService` under `/api/admin/v1/reports/*`, protected by the existing `AdminOnly` policy and standard rate-limit policy. Keep the launch scope to revenue, occupancy, and popular-vehicles reports using existing reservations, payments, and fleet tables.

**Rationale:**

- Keeps operational reports inside the existing monolith and admin API boundary.
- Avoids new persistence or projection infrastructure before launch.
- Preserves a clear deferred boundary for settings persistence and maintenance completion, which require separate configuration/migration decisions.

**Consequences:**

- The reports API contract is now tracked in `docs/07_API_Contract_ENTERPRISE_FULL.md`.
- Richer analytics, export workflows, and accounting-grade ledgers remain post-launch scope.
- Frontend report screens should consume these endpoints through the existing admin API client/hook patterns when that integration is scheduled.

### 12.7 OpenAPI Dependency Security Override

**Context:** The backend API uses `Microsoft.AspNetCore.OpenApi` for development-only OpenAPI generation. On 8 July 2026, NuGet vulnerability scanning reported GHSA-v5pm-xwqc-g5wc / CVE-2026-49451 through the transitive `Microsoft.OpenApi` 2.0.0 package.

**Decision:** Keep the existing ASP.NET Core OpenAPI integration and add an explicit `Microsoft.OpenApi` 2.7.5 package reference in `backend/src/RentACar.API/RentACar.API.csproj` to force the patched 2.x line without changing runtime API behavior.

**Rationale:**

- Preserves the current `Microsoft.AspNetCore.OpenApi` 10.0.9 integration and generated OpenAPI behavior.
- Avoids a broader framework/package upgrade in a focused dependency-security slice.
- Uses the patched version identified by the GitHub advisory for the consumed 2.x package line.

**Consequences:**

- Backend dependency vulnerability checks must include transitive packages with `dotnet list backend\RentACar.sln package --include-transitive --vulnerable`.
- Future ASP.NET Core package upgrades should verify whether the explicit `Microsoft.OpenApi` override is still needed or can be removed after the upstream transitive dependency advances.
- As of 8 July 2026, backend restore/build/test passed after the override, and `dotnet list ... --vulnerable` reported no vulnerable backend packages.

### 12.8 Reservation Extra Options Persistence Boundary

**Implementation status (2026-07-11):** Accepted and implemented through the admin and public booking phases on `codex/reservation-extra-options`. The relational catalog, session-bound Redis quote, unique reservation `quote_id`, versioned `jsonb` pricing snapshot, immutable selected-extra rows, five-locale admin/public surfaces, and legacy adapter now follow this boundary. Local Docker, the complete section 6.6 Chromium matrix, and PR #386 CI pass. Review follow-up maps invalid legacy quantities to HTTP 400, applies campaigns after both legacy-adapted and quote-backed generic extras enter the authoritative subtotal, reconciles persisted selections against the fresh catalog, aggregates repeated legacy URL codes, and requires customer confirmation whenever refreshed selection or quote terms change. This records implementation conformance, not release approval. Aikido, deployment/rollback, and the legacy-adapter production observation gate remain open in `docs/17_Reservation_Extra_Options_Implementation.md`.

The current comprehensive continuation handoff is `C:\Users\muham\AppData\Local\Temp\2026-07-11-203021-reservation-extra-options-final-implementation-handoff.md`; it consolidates the Phase 1-5 implementation boundary, the first seven section 6.6 acceptance rows, and the still-open release gates without replacing this ADR as the architectural source of truth.

**Context (decision time):** The public booking flow owned a hard-coded list of reservation extras and passed legacy extra counts into pricing. The approved replacement needed an admin-managed, five-locale catalog while preserving historical reservation pricing, legacy clients, and server authority over all monetary values.

**Decision:** Keep the feature inside the existing Core/API/Infrastructure monolith and introduce a normalized reservation-extra catalog with immutable reservation snapshots. Persist catalog options, translations, vehicle-group assignments, and selected reservation extras in four relational tables. Store the complete versioned pricing snapshot as `jsonb` on `reservations`, add nullable unique `quote_id` as the replay barrier, and use PostgreSQL `xmin` for catalog write concurrency.

Built-in option identifiers are deterministic. The additive migration assigns built-ins to vehicle groups present at migration time. A bounded startup backfill may assign them only when none of the built-ins has any assignment; ordinary vehicle-group creation does not silently expand option availability. Used options are protected from hard deletion by the selected-extra foreign key, while reservation deletion cascades its immutable snapshots.

The catalog application boundary remains in `RentACar.API/Services`, following the repository's established application-service placement. `IReservationExtraOptionCatalogService` is the single write/read boundary over `IApplicationDbContext`. Admin mutations are exposed only under `api/admin/v1/reservation-extra-options` with `AdminOnly`; localized public reads are exposed under `api/v1/reservation-extra-options`. Both surfaces use the standard rate-limit policy and disable response caching. The client never supplies code, activation state, archive state, localized snapshot payloads, or authoritative monetary values outside the dedicated server-owned catalog fields.

Catalog lifecycle is explicit: create produces an inactive draft, activation requires five complete translations plus at least one existing vehicle-group assignment, archived rows cannot be edited, and restore always returns to inactive draft. Unused rows may be deleted; used rows archive. A reference that races the hard-delete attempt is treated as archive after the PostgreSQL restrict violation, preserving the immutable reservation snapshot. Mutation audit rows share the catalog save boundary and contain stable action names, option identity, and changed field names without localized bodies or customer data.

**Rationale:**

- Keeps catalog querying relational and indexable without reconstructing historical prices from mutable option rows.
- Preserves a truthful total-only fallback for pre-migration reservations and an exact snapshot source for new-format reservations.
- Makes duplicate quote consumption and duplicate reservation-option snapshots database-enforced invariants.
- Keeps migration and rollback additive until new-format data exists, after which forward-fix or verified restore is safer than schema removal.
- Avoids a new service or dependency before the existing booking, pricing, admin, and observability boundaries are proven.

**Consequences:**

- Phase 1 is implemented by migration `20260709204616_AddReservationExtraOptions`; detailed evidence is in `docs/17_Reservation_Extra_Options_Implementation.md` section 3.5.
- Phase 2 is implemented by the dedicated catalog service and admin/public controllers; detailed evidence is in `docs/17_Reservation_Extra_Options_Implementation.md` section 4.6.
- Phase 3 is implemented by the generic calculator, session-bound Redis quote store, flat quote endpoint, quote-aware reservation transaction, legacy adapter, and snapshot-backed reads; detailed evidence is in `docs/17_Reservation_Extra_Options_Implementation.md` section 5.8.
- Admin writes update the parent catalog row when translations or assignments change so `xmin` advances; stale writes map to `409`, invalid input to `400`, and missing admin records to `404`.
- Phase 3 quote and reservation creation never accept client prices, totals, pricing modes, localized text, or snapshot payloads. Redis claims are owner-bound, and database replay requires retained quote/session/input validation before an existing reservation DTO is returned.
- Quote pricing applies percentage/fixed campaigns to the authoritative subtotal after generic catalog extras are included. Public recovery may retry automatically only when option identity, quantity, version, unit price, pricing mode, and final quote total remain unchanged; any changed term requires explicit customer confirmation.
- Public/admin catalog and generic quote/reservation contracts are implemented. Admin and public frontend phases remain governed by `docs/16_Reservation_Extra_Options_Plan.md`.
- Full Docker browser evidence remains open until the admin and public UI phases are implemented.
- The repository-required Aikido full-content scan remains a separate release gate when the MCP scanner is available.
## 13. Security Boundary Decisions (12 July 2026)

### 13.1 Verified Guest-Account Claim

Existing passwordless guest customers are not upgraded by knowledge of an email address. Registration returns a generic response and issues a purpose-specific, hashed, expiring, single-use `CustomerAccountClaimToken`; installing the first password requires possession of the emailed token. Requests are throttled against the normalized customer identity with a five-minute cooldown, and the database permits at most one active token per customer. Issuing a replacement token supersedes older active tokens. Raw tokens are never persisted. Expired, consumed, and superseded records are removed by the worker in bounded batches after a 14-day retention window.

This implementation remains as retained defense-in-depth code, but it is not a supported public contract for the current release. The public header no longer offers customer sign-in, the public registration and localized account-claim pages return `404`, the same-origin registration/claim proxies return empty `404` responses without forwarding, and the API rejects registration/claim paths before controller, idempotency, persistence, or background-job work. Existing customers may still use the direct customer login route; reopening registration or claim requires a separate product, security, notification-delivery, and deployment decision.

### 13.2 Public Reservation Boundary

Unauthenticated lookup returns only `PublicReservationSummaryDto`, an explicit allowlist without internal identifiers, customer/driver PII, plate, notes, hold data, or provider metadata. The route is strict-rate-limited and non-cacheable. Anonymous cancellation is removed; customer cancellation remains behind `CustomerOnly` ownership checks and admin cancellation remains admin-only.

### 13.3 Payment Fail-Closed Boundary

No payment provider is selected at this stage. Production therefore uses the explicit `Disabled` provider with `Payment:EnablePayments=false`; public intent creation, 3DS return, provider webhook processing, and admin payment retry continue to fail closed with `503` before reaching payment services. `DisabledPaymentProvider` is a distinct DI target rather than a Mock fallback and cannot create, verify, authenticate, refund, release, capture, or report a successful payment operation. Production payment configuration remains bound and validated with `ValidateOnStart`: missing, Mock, unknown, sandbox, incomplete Iyzico, Iyzico with payments enabled, or Disabled with payments enabled prevents startup while provider verification remains simulated. Positive controls are an explicit Disabled/false Production host without provider credentials, a fully configured payments-disabled Production Iyzico host, and an intentional Development Mock host. The current Release image reaches Docker health with Disabled/false against isolated PostgreSQL and Redis without synthetic Iyzico credentials; the earlier six-case negative container matrix remains the fail-fast proof for unsafe configurations. When a real provider is introduced, the browser return remains a navigation signal only: no paid transition is considered secure until provider evidence is verified server-to-server and bound to provider intent, reservation, amount, currency, status, and unique transaction/event identity.

### 13.4 Acceptance State

These decisions are implemented for the fail-closed public membership boundary, public reservation exposure/cancellation containment, explicit Disabled-provider payment containment, and production configuration validation. On 17 July 2026, the no-membership option was implemented in source and locally acceptance-proven: the public login entry point is absent; registration and five localized claim pages return `404`; frontend and backend registration/claim endpoints return `404` for exact, case-variant, and trailing-slash paths; and database/background-job fingerprints show no customer or job side effects. Existing-customer login remains available through its direct route. PR #413 head `5039c6028f1c21c8bd5aaecbb1cb3cc5e996ccee` was squash-merged to `main` as `fb7ca83e01599556ea9b06d24d9c570a4d0a111b`; post-merge CI/security workflows and the GHCR image push succeeded. After an operator-triggered Dokploy Compose deployment of that commit, cache-bypassed HTTP checks and a real Chromium pass confirmed `404` for all five localized account-claim routes, `/dashboard/register/v1`, and both public register/claim proxies; `/dashboard/login/v1` remained directly reachable with `200`, while the public homepage exposed no customer-login link. Live proxy checks used empty JSON bodies and did not create or mutate production data. The source, local-acceptance, and deployed-public membership gates are closed. Independent exact/case/trailing-slash proof against the internal backend container, container metadata/log inspection, and production database/job-count evidence remain unreviewed. Future automatic email is intended instead for reservation lifecycle notifications, whose provider and exact event matrix remain undecided.

No Resend provider or credential is currently part of the supported deployment. The historical Resend-shaped scanner match was traced to an ignored generated `frontend/tsconfig.tsbuildinfo` cache and then copied into committed Ship Safe artifacts. It had no source, environment, deployment, or account anchor, and the repository owner confirmed that Resend had not been configured, so Resend rotation is not applicable. The public reservation boundary is also locally acceptance-proven: the production-like Chromium flow captured the exact allowlisted response through all five localized confirmation pages, and database `status`/`xmin`/`updated_at` fingerprints proved that anonymous and non-owner cancellation attempts performed no write while authenticated owner cancellation remained functional. The reachable paid-transition attack path is contained while payments remain disabled, but payment-integrity acceptance is deferred until a real provider contract and sandbox evidence exist. The Dokploy deployment of `main` commit `d0a7990` passed the public Disabled-mode acceptance matrix: public browsing and settings remained available while intent creation, 3DS return, and webhook entry points failed closed with `503`. This closes the Disabled-mode deployment/public-containment gate only; it does not establish release readiness, real-provider integrity, or independently captured container-log/image-digest evidence. Canonical implementation and remaining evidence gates are tracked in `docs/18_Codex_Security_Findings_Implementation.md`.

The focused final validation completed on 16 July 2026 with the application-under-test at `202074f`; the corrected committed account-claim browser harness was then re-run from `9420446` against the same Docker acceptance stack. It found current counterevidence for the original guest-account claim, public reservation disclosure/cancellation, production Mock fallback, Dependabot auto-merge, and enabled callback attack paths. On 17 July, the selected alternative closure path disabled the public membership surface while retaining the earlier claim controls as defense in depth. The replacement Chromium contract proves registration/claim pages and endpoints fail closed, the homepage exposes no customer login link, and rejected requests create neither customer nor background-job records. Payment requests and forged callbacks remain fail-closed with no database mutation while payments are disabled. The Resend and Upstash portions of the historical scanner incident remain not applicable for credential rotation. Payment-integrity decisions, authoritative provider verification, mismatch/replay negatives, sandbox proof, deployed revalidation of the remaining original attack paths, and independent container evidence remain external or deferred gates. Future reservation-notification email provider and event selection remain a separate product decision.

### 13.5 Repository Change Governance

Direct change control for `main` is implemented by the active GitHub repository ruleset `Protect main - solo developer` (ID `18985047`), scoped to `refs/heads/main`. GitHub Actions workflows produce build, test, container, secret-scan, and CodeQL statuses; the ruleset is the enforcement layer that requires those statuses before merge.

The solo-developer policy requires a pull request, resolution of review threads, a branch updated against `main`, and these seven GitHub Actions checks: `Backend Unit Tests`, `Backend Integration Tests`, `Frontend Lint, Test & Build`, `Docker Build`, `Gitleaks`, `CodeQL Analyze (csharp)`, and `CodeQL Analyze (javascript-typescript)`. The required approving-review count is zero, so the repository does not depend on a second maintainer, but merge remains an explicit manual decision. Only squash merge is allowed. The ruleset has no bypass actors and blocks deletion and non-fast-forward updates.

Dependabot PRs use the same boundary. Automatic dependency merging is intentionally absent; an update must be current, pass all required checks, have its review threads resolved, and receive a manual merge or close decision. Ruleset enforcement is repository-governance evidence only and does not establish application security or release readiness.

Dependency-remediation closure is evidence-based and asynchronous. A patched manifest/lockfile, successful Node 22 CI, and a default-branch SBOM that lists only patched versions prove that the repository dependency graph has moved to the intended versions. They do not, by themselves, prove that GitHub has reconciled the corresponding Dependabot alert records. A security alert remains open until the live alert source reports it as fixed, or an explicit reviewed risk decision records a different disposition. Manual dismissal must not be used to represent a technical fix.

If the default-branch SBOM is patched while alert records remain open, the state is recorded as `remediation merged; alert reconciliation pending`. The repository must preserve the patched dependency graph, retain the green PR and post-merge workflow evidence, and investigate GitHub reconciliation if the records persist. When a complete live dependency-graph traversal also reports only patched requirements while the alert objects retain old `vulnerableRequirements`, the mismatch is classified as external alert-record reconciliation lag rather than residual repository exposure. Do not create package or lockfile churn to force a refresh. Re-query after a 12-24 hour asynchronous window; if the mismatch persists, request backend resynchronization only as an explicitly user-authorized external action. A prepared support draft is not evidence that a support request was submitted. An unavailable auxiliary audit source, including an HTTP `410` response from a retired endpoint, is recorded as no result and never as a clean scan.

The 16 July 2026 reconciliation re-check satisfied the live-alert gate for the tracked dependency slice: all 11 original records report `fixed`, with null dismissal and auto-dismissal fields, and the fresh default-branch SBOM contains only the five patched target versions. No package churn, manual dismissal, backend resynchronization, or support request is required. A complete Dependabot pull-request lifecycle created after ruleset activation is still retained as an operational-assurance objective rather than an application-security release blocker.
