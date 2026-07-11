# Reservation Extra Options Implementation

**Created:** 2026-07-09
**Scope:** Implementation sequence for admin-managed reservation extra options
**Decision source:** [16_Reservation_Extra_Options_Plan.md](16_Reservation_Extra_Options_Plan.md)
**Verification gate:** Automated test suite plus Docker Desktop browser validation
**Implementation status:** Phases 1-5 are implemented; the complete local browser matrix and PR #386 CI pass. Aikido, deployment/rollback rehearsal, and the production legacy-adapter observation gate remain open.

## 1. Objective

Implement the approved reservation-extra catalog without changing the established public/admin design split or trusting browser-provided prices. Delivery must proceed in small, independently testable slices so database, pricing, admin, and booking-flow failures remain easy to isolate.

This document is executable guidance. When it conflicts with a speculative implementation preference, the decisions and invariants in the plan document win.

## 2. Preflight and Change Boundaries

Before editing:

1. Confirm the real repository root and inspect `git status --short`.
2. Preserve all unrelated tracked and untracked user changes.
3. Read the current reservation, pricing, public booking, admin settings, and test implementations named below.
4. Confirm migration ordering and the current EF model snapshot.
5. Do not install a package; existing .NET, React, SWR, Zustand, next-intl, shadcn, and Lucide facilities are sufficient.

Primary existing anchors:

- `backend/src/RentACar.API/Services/PricingService.cs`
- `backend/src/RentACar.API/Services/ReservationService.cs`
- `backend/src/RentACar.API/Contracts/Reservations/`
- `backend/src/RentACar.Infrastructure/Data/RentACarDbContext.cs`
- `frontend/app/(public)/[locale]/booking/step3/page.tsx`
- `frontend/app/(public)/[locale]/booking/step4/page.tsx`
- `frontend/hooks/useBooking.ts`
- `frontend/app/(admin)/dashboard/(auth)/settings/layout.tsx`

Do not mix unrelated admin UX, pricing-rule, campaign, payment-provider, or public-content refactors into this change.

## 3. Phase 1 - Domain Model, EF Mapping, and Migration

### 3.1 Domain Types

Add:

- `ReservationExtraOption`
- `ReservationExtraOptionTranslation`
- `ReservationExtraOptionVehicleGroup`
- `ReservationSelectedExtra`
- `ReservationPricingSnapshotV1`
- `ReservationExtraPricingMode` with `PerDay` and `PerRental`

Add navigation collections to `Reservation`, `VehicleGroup`, and `ReservationExtraOption` only where needed for querying and persistence. Keep aggregate writes in an application service rather than putting pricing behavior in entities.

### 3.2 EF Configuration

Add explicit configurations for:

- table and column names;
- composite keys for translations and vehicle-group assignments;
- decimal precision;
- supported lengths;
- unique index on option code;
- indexes for `IsActive + IsArchived + SortOrder`, translation locale, vehicle-group lookup, and reservation snapshot lookup;
- cascade behavior for unused option dependents;
- cascade assignment deletion when either an unused option or vehicle group is deleted;
- restricted option deletion when a reservation snapshot exists;
- cascade snapshot deletion when a reservation is deleted;
- unique `ReservationId + ExtraOptionId` selected-extra rows;
- nullable unique `Reservation.QuoteId` as the database replay barrier;
- `ReservationPricingSnapshotV1` persisted as versioned `jsonb` on the reservation;
- `Version: uint` mapped to PostgreSQL `xmin` with `IsRowVersion()`.

Expose new sets through both `RentACarDbContext` and `IApplicationDbContext` if the service layer requires them.

### 3.3 Migration and Seed

Generate one migration after the model is complete. It must:

1. Create the four tables and all constraints/indexes.
2. Use deterministic GUIDs for the four built-in options.
3. Seed:
   - `child_seat`: 75 TRY, `PER_DAY`, `baby`;
   - `additional_driver`: 150 TRY, `PER_RENTAL`, `users`;
   - `gps`: 8 TRY, `PER_DAY`, `navigation`;
   - `wifi`: 12 TRY, `PER_DAY`, `wifi`.
4. Copy the current TR/EN/DE/RU/AR names and descriptions from frontend locale messages into translation rows.
5. Assign every seeded option to every vehicle group present during migration using bounded PostgreSQL `INSERT ... SELECT` SQL with constant identifiers.
6. Set all four seed records active only when at least one assignment exists; otherwise leave them inactive.
7. Leave existing reservations untouched.

After optional runtime inventory seeding, run an idempotent one-time backfill only when all four built-ins still have no assignments. Assign the current groups and activate the built-ins after validation. Do not auto-assign vehicle groups created later through normal admin workflows.

The down migration is a pre-data development aid only. Once any new-format reservation contains selected-extra or pricing snapshots, production rollback must use frontend rollback, forward-fix, or verified backup restore; it must never drop the new schema.

### 3.4 Phase 1 Tests and Exit Gate

- EF model test verifies keys, unique indexes, precision, and delete behavior.
- Integration test verifies seed records, five translations, and group assignments.
- Fresh-database test verifies late inventory seed/backfill and activation behavior.
- Integration test proves a used option cannot be hard deleted.
- Integration test proves duplicate reservation/option snapshots are rejected.
- `dotnet build backend/RentACar.sln --no-restore` passes before Phase 2.

### 3.5 Phase 1 Completion Evidence - 2026-07-09

- Added the catalog, translation, vehicle-group assignment, selected-extra, and versioned pricing-snapshot domain types.
- Added explicit PostgreSQL mappings, constraints, indexes, `xmin` concurrency, nullable unique `QuoteId`, and the `jsonb` pricing snapshot.
- Generated migration `20260709204616_AddReservationExtraOptions` with deterministic built-ins, 20 copied locale rows, migration-time group assignment, and conditional activation.
- Added the idempotent startup backfill for the zero-assignment late-inventory case; groups created through normal workflows after initial assignment remain unassigned by design.
- Focused EF model tests passed: 2/2.
- Focused PostgreSQL integration tests passed: 5/5, covering seed shape, late backfill/idempotence, restricted hard delete, duplicate selected extras, and duplicate quote replay protection.
- Full backend validation passed: build with 0 warnings/0 errors, 685 `RentACar.Tests`, and 39 `RentACar.ApiIntegrationTests`.
- `dotnet format ... --verify-no-changes` passed for all changed first-party C# files.
- Docker Desktop full compose build passed. API `http://localhost:5000/health` and web `http://localhost:3001/tr` returned HTTP 200.
- Running Docker PostgreSQL confirmed 4 active built-ins, 20 translations, 8 migration-time assignments, and latest migration `20260709204616_AddReservationExtraOptions`.
- Docker browser validation remains open because Phase 1 adds no customer/admin UI; the full browser matrix belongs after Phases 4 and 5.
- Aikido MCP was not available in the active tool set, so the required full-content security scan remains an open release blocker. Setup guide: https://help.aikido.dev/ide-plugins/aikido-mcp

## 4. Phase 2 - Catalog Service and Admin API

### 4.1 Contracts

Add a dedicated reservation-extra contracts namespace containing:

- admin list/detail DTOs;
- create and full-update requests;
- status request;
- restore request/result;
- localized translation DTO;
- public catalog response and item DTO;
- delete result with `deleted` or `archived` disposition.

Wire enums serialize as `PER_DAY` and `PER_RENTAL`. Return the PostgreSQL `xmin`-backed `Version` as the public/admin concurrency value; return `UpdatedAt` separately for display.

### 4.2 Catalog Service

Create a small service responsible for:

- paginated admin reads and filters;
- localized public group reads;
- code generation for new options;
- normalization and validation;
- create/update/status/delete/archive behavior;
- group-assignment replacement in one transaction;
- archive and restore behavior;
- atomic hard-delete with concurrent-reference conflict handling;
- audit events;
- optimistic concurrency handling.

Validation rules are fixed by the plan:

- price 0 through 1,000,000;
- maximum quantity 1 through 20;
- sort order 0 through 9,999;
- icon key from the allowlist;
- distinct, existing vehicle-group identifiers;
- one translation per supported locale;
- active options require all five non-empty names/descriptions and at least one assignment;
- archived options cannot be edited directly and may only be restored to inactive draft.

For new codes, generate `extra-{guid}` server side and keep it immutable. Do not derive the code from a translation because inactive drafts may not yet have a Turkish name.

Every translation or assignment mutation must update the parent option row in the same transaction so `xmin` advances. A stale `Version` returns `409`.

### 4.3 Controllers

Add:

- a public controller under `api/v1/reservation-extra-options` with standard rate limiting;
- an admin controller under `api/admin/v1/reservation-extra-options` with `AuthPolicyNames.AdminOnly` and standard rate limiting.

Add an explicit restore endpoint. Restore sets `IsArchived = false` and keeps `IsActive = false`; it never bypasses activation validation.

Map malformed input to `400`, missing admin records to `404`, and stale versions to `409`. Do not expose EF exceptions or internal identifiers beyond contract fields.

### 4.4 Audit Events

Use the existing audit infrastructure and stable action names:

- `ReservationExtraOptionCreated`
- `ReservationExtraOptionUpdated`
- `ReservationExtraOptionActivated`
- `ReservationExtraOptionDeactivated`
- `ReservationExtraOptionAssignmentsChanged`
- `ReservationExtraOptionDeleted`
- `ReservationExtraOptionArchived`
- `ReservationExtraOptionRestored`

Record option ID/code and changed field names, not customer data or full localized bodies.

### 4.5 Phase 2 Tests and Exit Gate

- Controller authorization proves Admin and SuperAdmin access and rejects unauthenticated/customer users.
- Service tests cover draft creation, activation completeness, all validation bounds, immutable GUID-based codes, `xmin` concurrency conflict, archive/restore, and delete disposition.
- Two-context integration tests prove stale writes fail and child mutations advance the parent option version.
- Public query tests cover locale, group, status, archive, order, and empty results.
- Audit tests cover every mutation action.
- Focused tests and backend build pass before pricing integration starts.

### 4.6 Phase 2 Completion Evidence - 2026-07-10

- Added dedicated admin/public reservation-extra contracts with server-generated immutable codes, PostgreSQL `xmin` versions, and `PER_DAY` / `PER_RENTAL` JSON serialization.
- Added the catalog service through `IApplicationDbContext` with bounded validation, five-locale activation completeness, vehicle-group replacement, archive/restore, conditional hard delete, and parent-row version bumps for child mutations.
- Added `AdminOnly` admin endpoints and anonymous public catalog reads under the standard rate-limit policy; both catalog surfaces return `Cache-Control: no-store`.
- Added PII-free audit events for create, update, activation, deactivation, assignment changes, delete, archive, and restore. Audit payloads contain option ID/code and changed field names, not localized bodies.
- Added focused service/controller coverage for every validation boundary, immutable code generation, lifecycle behavior, all audit actions, status/filter/order/empty public results, and `400` / `404` / `409` controller mappings.
- Added real API authorization tests proving Admin and SuperAdmin access while rejecting unauthenticated and customer principals.
- Added real PostgreSQL tests proving two-context stale writes fail, child mutations advance the parent `xmin`, and a reference inserted during hard delete converts the outcome to archive without losing the snapshot.
- Focused `ReservationExtraOption` suites passed: 27 `RentACar.Tests` and 14 `RentACar.ApiIntegrationTests`.
- Full backend validation passed after the final changes: build with 0 warnings/0 errors, 710 `RentACar.Tests`, and 48 `RentACar.ApiIntegrationTests`.
- Docker PostgreSQL and Redis were used for integration tests. Browser validation remains open because Phase 2 adds API surfaces but no admin/customer UI.
- CI has not run for this local branch.
- Aikido MCP is not available in the active tool set, so the required full-content scan remains an open release/security blocker. Setup guide: https://help.aikido.dev/ide-plugins/aikido-mcp

## 5. Phase 3 - Generic Quote and Reservation Persistence

### 5.1 Selected-Extra Input

Add a shared input contract:

```text
optionId: Guid
quantity: int
optionVersion: uint
```

Reject duplicate option IDs rather than merging them. Load all requested records and assignments in one database query; never query once per selection.

### 5.2 Extra Calculation

Extract a focused calculator/validator from `PricingService` or add a small collaborator owned by pricing. It returns authoritative localized line items and total:

- per day: unit price × current pricing-engine rental days × quantity;
- per rental: unit price × quantity;
- round using existing pricing conventions;
- reject inactive, archived, stale, unassigned, missing, or over-limit options when issuing the quote.

Remove fixed child-seat and additional-driver constants from the new generic path. Keep them only inside the temporary legacy adapter until that compatibility path is removed.

### 5.3 Quote Endpoint

Add `POST /api/v1/pricing/quote` without removing the current GET breakdown endpoint. Add `IReservationQuoteStore` in Core and a Redis implementation in Infrastructure, reusing the established Redis/session patterns.

The quote endpoint:

1. Normalizes request values.
2. Runs existing base/campaign/fee pricing.
3. Runs generic extra validation/calculation.
4. Builds `ReservationPricingSnapshotV1` and the selected-extra snapshot candidates.
5. Stores an opaque `ReservationQuoteV1` in Redis for 15 minutes, bound to `X-Session-Id`.
6. Returns existing price fields, `extraItems`, `ExtrasTotal`, `FinalTotal`, `quoteId`, and `expiresAtUtc`.

Quote and catalog responses use `Cache-Control: no-store`. Redis values must not contain customer, driver, card, cookie, or authentication-token data. Store only normalized price-driving inputs, selected options, the pricing snapshot, issue/expiry time, and a one-way hash of the public session identifier.

### 5.4 Reservation Creation

Extend reservation creation and unpaid-request creation with locale and `QuoteId`. Selected extras are supplied to the quote endpoint, not trusted again at reservation creation.

Around the existing create transaction:

1. Resolve an existing reservation by unique `QuoteId`; return it for a valid retry.
2. Load the quote and verify session binding, expiry, normalized vehicle/office/date/campaign inputs, and available state.
3. Atomically claim the Redis quote with the request/idempotency key and a bounded claim TTL.
4. Reload selected options in one query and revalidate active/archive status, vehicle-group assignment, and maximum quantity.
5. Honor quote prices for the 15-minute validity window when only catalog prices changed.
6. Return `409` and create nothing when claim, availability, expiry, session binding, or replay checks fail.
7. Create the reservation with the quote's authoritative final total and unique `QuoteId`.
8. Add one `ReservationSelectedExtra` snapshot per selected option and persist `ReservationPricingSnapshotV1`.
9. Save and commit the database transaction.
10. Mark the quote consumed with the reservation ID. If the transaction fails, release only the claim owned by this request.

Client content and prices must never enter snapshot columns. If post-commit Redis finalization fails, the unique database `QuoteId` remains authoritative; retry/reconciliation marks the quote consumed and returns the existing reservation instead of creating another.

### 5.5 Legacy Adapter

- If `QuoteId` is absent, convert `ExtraDriverCount` and `ChildSeatCount` to seeded option selections and calculate through the current generic catalog path.
- If either legacy count is non-zero and `QuoteId` is also present, return `400`.
- The adapter uses current catalog prices and validation, not retired constants.
- Built-in codes remain immutable and non-deletable while compatibility mode is active.
- Emit structured legacy-use, unknown-code, and legacy-reservation events.
- Add a deprecation marker without removing fields; removal requires 14 consecutive production days with zero legacy use and a separate cleanup release.

### 5.6 Reservation Reads

Extend admin/customer reservation DTOs with snapshot-based selected extras, a snapshot-backed full price breakdown, and `breakdownSource`. New reservations read every monetary field from `ReservationPricingSnapshotV1`. Pre-migration reservations return an empty selected-extra list and `LEGACY_TOTAL_ONLY`; do not add snapshot extras to the current `BaseTotal = TotalAmount` fallback.

### 5.7 Phase 3 Tests and Exit Gate

- Per-day, per-rental, free, quantity, maximum, duplicate, inactive, archived, stale, and wrong-group cases.
- GPS and Wi-Fi are included in final totals.
- Client-supplied price-like fields cannot influence calculation.
- Quote expiry, session mismatch, replay, deactivation, archive, wrong group, and reduced maximum prevent reservation creation.
- Price-only changes preserve an unexpired quote price.
- Reservation, selected-extra rows, and full-pricing snapshot commit or roll back together.
- Full snapshot reads reproduce all monetary fields without double counting; legacy reads are explicitly total-only.
- Snapshot reads remain unchanged after catalog update/archive.
- Redis quote tests cover atomic claim ownership, claim expiry/release, two concurrent requests with different idempotency keys, and post-commit finalization failure; none may create a duplicate reservation.
- Legacy-only requests map correctly, mixed requests fail, and structured usage events are emitted.
- Existing reservation, pricing, payment, and idempotency regression tests pass.

### 5.8 Phase 3 Completion Evidence - 2026-07-10

- Added the shared server-owned selected-extra input contract (`optionId`, `quantity`, and `optionVersion`) and a focused generic calculator that loads requested options, localized text, and group assignments without per-selection queries.
- Added per-day, per-rental, free, multi-quantity, duplicate, missing, stale, inactive, archived, wrong-group, maximum-quantity, and structural revalidation behavior. Price and `xmin` changes alone preserve the issued quote price; pricing-mode, activation, archive, assignment, and reduced-maximum changes invalidate submission.
- Added flat `POST /api/v1/pricing/quote` responses with the existing monetary fields plus authoritative `extraItems`, `quoteId`, and `expiresAtUtc`. The endpoint validates vehicle groups, offices, campaigns, dates, driver age, locale, and selected extras server side and returns `Cache-Control: no-store`.
- Added `ReservationQuoteV1` and `IReservationQuoteStore` in Core plus a StackExchange.Redis implementation. Quote payloads contain normalized price-driving inputs, selected snapshots, the complete pricing snapshot, timestamps, and a SHA-256 session hash; customer, driver, card, cookie, and authentication values are excluded.
- Added atomic Redis claim acquisition, owner-only release/finalization, bounded claim expiry, consumed-state replay blocking, and database reconciliation. Two concurrent claim owners cannot both win.
- Extended draft and unpaid reservation creation with `Locale` and `QuoteId`. Quote/session/input/expiry/current-option checks run before persistence; the reservation, unique `QuoteId`, selected-extra snapshots, and `ReservationPricingSnapshotV1` commit in the same relational transaction.
- Database replay resolution now revalidates the retained quote session hash and normalized booking inputs before returning an existing reservation. This closes the cross-session DTO disclosure risk that an unconditional `QuoteId` lookup would create.
- Added the legacy adapter for child-seat and additional-driver counts. Mixed `QuoteId` plus non-zero legacy counts fail; legacy-only inputs use current catalog records and emit a structured adapter-use event without removing the legacy fields.
- Reservation reads now return `selectedExtras`, snapshot-backed complete price breakdowns, and `breakdownSource: SNAPSHOT`; pre-Phase-3 rows remain explicit `LEGACY_TOTAL_ONLY` with no reconstructed historical extras.
- Focused Phase 3 validation passed: 20 `RentACar.Tests` and 3 real Redis/PostgreSQL `RentACar.ApiIntegrationTests`, including the full quote-to-reservation endpoint, selected-extra/snapshot persistence, different-idempotency-key replay, claim ownership, claim expiry, and cross-session rejection.
- Final full backend evidence for this slice: build 0 warnings/0 errors, 730 `RentACar.Tests`, and 51 `RentACar.ApiIntegrationTests` after the final rerun.
- `dotnet format --verify-no-changes` passed for all 25 Phase 3 changed C# files. The repository-wide format command still reports pre-existing out-of-scope line-ending/charset findings in `BcryptPasswordHasherTests.cs` and `StatusEnumTests.cs`; those files were not modified.
- Docker PostgreSQL and Redis backed the API integration suite. Phase 3 adds no frontend route, so browser workflow evidence remains open for Phases 4-5.
- CI has not run for this local branch. Aikido MCP remains unavailable in the active tool set, so the required full-content scan remains an open release/security blocker. Setup guide: https://help.aikido.dev/ide-plugins/aikido-mcp

## 6. Phase 4 - Admin Frontend

### 6.1 API and Hooks

Add admin types, API functions, and SWR hooks for list/create/update/status/delete/restore. Use a stable key that includes filters. After a successful mutation, revalidate the list and close the editor only after the returned DTO is available.

Do not implement optimistic content updates. Use server versions and surface `409` with an explicit reload choice.

### 6.2 Settings Route

Add `/dashboard/settings/reservation-extras` and register `reservation-extras` in the settings tabs. Keep the existing horizontal overflow behavior on narrow viewports.

Use existing admin primitives for page header, filters, loading/error/empty states, and table shell. The page includes:

- search input;
- active/inactive/archived filter;
- vehicle-group filter;
- option table with name, price rule, maximum quantity, groups, and status;
- create/edit action;
- explicit status action;
- delete/archive action with confirmation.
- restore-to-draft action for archived records.

### 6.3 Editor

The editor contains:

- TR/EN/DE/RU/AR tabs;
- name and description fields per locale;
- completion badges per locale;
- price and pricing-mode inputs;
- maximum quantity;
- vehicle-group multi-select;
- icon allowlist picker;
- sort order;
- readiness summary;
- save-draft and activate behaviors.

Activation remains a server decision. The frontend mirrors validation for immediate feedback but displays server errors as authoritative.

### 6.4 Phase 4 Tests and Exit Gate

- API client unwrap/error tests.
- Hook key and mutation delegation tests.
- Page loading, error, empty, filter, and populated states.
- Five-locale completion and activation blocking.
- Create/edit/status/delete/archive/restore flows.
- Stale-version recovery.
- Accessible names for every icon-only control.
- Focused Vitest, TypeScript, and lint pass.

### 6.5 Phase 4 Completion Evidence - 2026-07-11

- Added explicit admin reservation-extra contracts and API functions for filtered list, create, update, status, delete/archive, and restore operations. Admin responses are unwrapped consistently, delete sends the server version as a query value, and restore returns the server DTO used for subsequent UI state.
- Added an SWR hook with a stable primitive key containing search, status, vehicle group, archived inclusion, page, and page size. Mutations delegate to the API without optimistic content writes.
- Added `/dashboard/settings/reservation-extras` to both the settings tab strip and the admin sidebar while preserving the existing narrow-viewport horizontal overflow behavior.
- Added the admin catalog page with search, status and vehicle-group filters; loading, error, empty, populated, and paginated states; accessible row actions; explicit activation/deactivation; confirmed server-decided delete/archive; restore-to-draft; and explicit stale-version reload handling.
- Added a localized editor with TR/EN/DE/RU/AR tabs, per-locale completion badges, bounded price/quantity/order inputs, pricing mode, icon allowlist, vehicle-group multi-select, readiness summary, draft save, activation, authoritative server errors, and `409` recovery. The editor closes only after the returned DTO is available and the list revalidation starts.
- Focused Phase 4 validation passed: 4 Vitest files and 15 tests covering API unwrap/error behavior, stable hook keys and mutation delegation, page states and lifecycle actions, five-locale readiness, draft/create/edit behavior, partial activation failure recovery, accessible controls, and stale-version recovery.
- Full frontend validation passed from a clean temporary dependency workspace created from the exact repository source and `pnpm-lock.yaml`: TypeScript, ESLint with 0 errors, 59 Vitest files with 269 tests, and the Next.js production build. The build emitted `/dashboard/settings/reservation-extras`.
- The full lint run retained one pre-existing out-of-scope warning in `components/public/SearchForm.test.tsx` for an unused ESLint disable directive. No Phase 4 file produced a lint warning.
- The repository `frontend/node_modules` tree was already incomplete and repeated in-place pnpm repair attempts did not materialize binaries. The clean validation workspace installed 910 locked packages successfully; SHA-256 comparison confirmed all 12 Phase 4 source/test/navigation files in that workspace matched the repository files exactly.
- `git diff --check` passed. No dependency, secret, backend authorization rule, public route, or Phase 5 booking-flow file changed in this phase.
- CI, Docker browser validation, the plan-required Aikido full-content scan, and the combined public booking workflow remain open. Phase 5 must start from the server-owned public catalog and quote contracts rather than duplicating pricing logic in the browser.

## 7. Phase 5 - Public Booking Flow

### 7.1 Booking Store

Add a dedicated booking selection type with option ID, quantity, option version, and non-authoritative display fields. Update `setExtras` to replace the full selection set and clear selections when the vehicle group changes.

Persist selections through the current Zustand storage mechanism. Add a store schema/version migration so stale legacy entries do not crash hydration.

### 7.2 Step 3

- Remove the local `ExtraOption` interface and hard-coded array.
- Fetch the public catalog using selected vehicle group and current locale.
- Key the SWR request by vehicle group and locale, use `Cache-Control: no-store`, and revalidate whenever either value changes.
- Render zero-to-maximum quantity controls.
- Show calculated display totals while marking the server quote as authoritative.
- Store selections and navigate without writing `extras` into the URL.
- Provide loading, retryable error, and empty-catalog states.
- Preserve driver/customer form data during retry or catalog refresh.

### 7.3 Step 4

- Remove the second hard-coded extra array.
- Read selections from the booking store.
- Request a quote on load and after campaign/selection changes.
- Render server `extraItems`, total, and quote-expiry state.
- Submit locale and `quoteId` with paid and unpaid reservation requests; do not resend trusted selection/pricing data.
- On a `409`, keep customer/driver/payment-method state and perform one catalog/quote refresh. If the refresh removes an option or changes its quantity, stop before resubmission and require explicit customer confirmation. If selections are unchanged, allow at most one automatic retry; a further conflict also requires confirmation.
- Do not create a payment intent until reservation creation succeeds with the accepted quote.

### 7.4 Legacy URL Compatibility

During the measured compatibility period, when the store has no selections and the URL contains legacy `extras`:

1. Load the current catalog.
2. Map only `child_seat`, `additional_driver`, `gps`, and `wifi` codes.
3. Use quantity one for each mapped code.
4. Ignore unknown codes and show a non-blocking warning.
5. Remove the parameter from newly generated navigation URLs.

### 7.5 Reservation Detail

Add an `Extra Options` card to admin reservation detail. Each row displays snapshot name, quantity, unit price/rule, and total. Render the complete price breakdown from the persisted snapshot and display a `legacy total only` indicator for pre-migration reservations. Archived/deleted catalog state must not affect the display.

### 7.6 Phase 5 Tests and Exit Gate

- Store hydration, replacement, vehicle-change clearing, and schema migration.
- Step 3 group-specific fetch, quantity boundaries, locale rendering, retry, and empty state.
- Step 4 quote request, expiry display, campaign refresh, paid/unpaid `quoteId` payload, one-retry `409` recovery, and payment ordering.
- Price-only edit retains the issued quote; deactivate/archive/group-removal invalidates it.
- Redis quote expiry, cross-session replay, and repeated-submit behavior.
- Legacy URL mapping and unknown-code warning.
- Admin reservation snapshot rendering.
- Public booking and admin page focused suites pass before the full suite.

### 7.7 Phase 5 Local Implementation Evidence - 2026-07-11

- Replaced the persisted legacy booking-extra shape with a dedicated selection contract carrying option ID, quantity, option version, and display-only catalog fields. The Zustand store now replaces selections atomically, clears them on vehicle-group changes, and migrates stale persisted state safely to schema version 2.
- Added a public no-store catalog/quote API client. Step 3 now queries the catalog by vehicle group and locale, exposes bounded quantity controls with loading/error/empty/retry states, maps only the four measured legacy URL codes when no current selection exists, and removes `extras` from all newly generated navigation URLs.
- Step 4 now requests the session-bound server quote on load, selection changes, and valid campaign changes; renders server `extraItems`, final total, and expiry state; and sends `quoteId`, locale, and the matching non-price booking inputs (driver age and coverage flag) with the same `X-Session-Id` and `Idempotency-Key` on paid or unpaid reservation creation. It does not resend trusted selection or pricing data.
- A reservation `409` refreshes the public catalog and quote while retaining customer, driver, payment-method, and card-form state. Changed selections stop before resubmission and require explicit customer confirmation; an unchanged selection set permits at most one automatic retry. Payment-intent creation still occurs only after a reservation succeeds with an accepted quote.
- Added the admin reservation-detail `Ek Seçenekler` card for immutable selected-extra snapshots, including quantity, unit pricing rule, total, and the `LEGACY_TOTAL_ONLY` fallback for pre-migration rows.
- Local validation ran from the clean locked workspace `C:\tmp\arac-kiralama-phase4-validation-20260711`: TypeScript passed; 6 focused Vitest files / 42 tests passed; full Vitest passed with 60 files / 274 tests; ESLint passed with 0 errors and one pre-existing warning in `components/public/SearchForm.test.tsx`; and the Next.js production build passed with Step 3, Step 4, and admin reservation-detail routes emitted. The full suite/build evidence preceded the final one-line `driverAge` request-alignment correction; after that correction, TypeScript and the focused Step 4 suite (15/15) passed. A repeat full-suite/build attempt made no progress within the local command timeout and is not claimed as final proof.
- Fresh Docker evidence now passes: all five Compose services started from rebuilt images; PostgreSQL/Redis were healthy; API `/health` and frontend `/tr` returned 200; `/` redirected 307 to `/tr`; and the production web build included the Phase 5 routes.
- Repository Playwright/Chromium booking/payment smoke passed 6/6 after stale E2E vehicle selectors, the unavailable `ayt` seed office, required driver fields, and disabled-online-payment expectations were aligned with the current application. A focused Docker browser proof also passed 6/6 for admin catalog load, selected child-seat quote rendering, removal of generated `extras`, quote-expiry status, terms validation, real unpaid creation/confirmation, and the new reservation's current no-extra/non-legacy admin detail.
- The real persistence pass exposed a PostgreSQL blocker outside the original frontend slice: driver birth/license dates arrived as `DateTimeKind.Unspecified`, causing a `timestamptz` write failure. `ApplyDriverSnapshot` now normalizes those values to UTC; a focused backend regression passed 1/1, the API image was rebuilt, and the browser reservation flow passed. Test-created local holds were cancelled after verification.
- Browser-client bootstrap still fails in the host environment because its generated ESM kernel calls CommonJS `require`, so the existing repository Playwright/Chromium runner provided the browser fallback. A continuation run passed the exact-current booking/payment/i18n/mobile Chromium bundle at 16/16 and added a self-cleaning authoring acceptance test proving incomplete activation blocking plus group-specific TR/EN/DE/RU/AR catalog visibility with `Cache-Control: no-store`; its unused option was deleted in `finally`, and PostgreSQL cleanup verification returned zero matching rows. The reservation-extra spec then expanded to 9/9: Step 3 covers loading, per-day/per-rental quantities and totals, error/retry/empty states, legacy warnings, and URL cleanup; Step 4 covers server quote/campaign/paid/unpaid ordering plus price-only quote preservation and bounded availability-conflict recovery. The conflict test exposed and closed a duplicate automatic quote refresh caused by the selection-store update racing the explicit first-`409` refresh. The corrected flow uses one catalog/quote refresh, retries once with the original idempotency key, stops on a second conflict, preserves payment method/card/terms state, and requires explicit quote review before a new submission. The ninth scenario uses a real unpaid reservation and PostgreSQL snapshot: it proves selected-extra/full-pricing values remain unchanged after a live catalog name/price edit, verifies every fee contributes once to the final total, and opens a real pre-migration row with the explicit `LEGACY_TOTAL_ONLY` warning. That pass exposed raw backend `baseTotal`/`finalTotal` fields bypassing admin-client normalization; `normalizeReservation` now maps the raw snapshot contract consistently, with focused admin API Vitest 13/13. The catalog mutation is restored and the created reservation is cancelled in `finally`. CI, Aikido, and the remaining section 6.6 scenarios stay open: expiry/cross-session/replay, extra-specific responsive layout, and durable console/network/screenshot evidence.
- The validated immutable-history closure handoff is `C:\Users\muham\AppData\Local\Temp\2026-07-11-194923-reservation-extra-options-immutable-history-handoff.md` (100/100). It supersedes the catalog-conflict handoff as the continuation entry point and routes the next acceptance slice to expired, cross-session, and replayed quote IDs without duplicate reservation creation.
- Quote-lifecycle acceptance is now implemented in `frontend/e2e/tests/reservation-extra-options.spec.ts`. The browser issues real quotes against Docker, expires a Redis quote key, submits another quote from a mismatched session, replays a consumed quote with a new idempotency key, and verifies PostgreSQL counts by the unique `quote_id`. Expired/cross-session requests return `409` with zero rows; replay returns the original reservation and leaves exactly one row. Deterministic cleanup cancels the created reservation and removes quote/claim/consumed keys. TypeScript, ESLint, Prettier, existing Chromium scenarios 9/9, focused quote-lifecycle Chromium 1/1, and focused .NET replay integration 1/1 pass. The combined file is deliberately run in separate clean API rate-limit windows because the local limiter is process-scoped. Section 6.6 now has seven checked workflow rows; extra-specific responsive/console/network/screenshot evidence, CI, Aikido, deployment/rollback, and production observation remain open.
- The current comprehensive continuation handoff is `C:\Users\muham\AppData\Local\Temp\2026-07-11-203021-reservation-extra-options-final-implementation-handoff.md`; it consolidates Phase 1-5 completion and all seven closed acceptance rows, then routes the next slice to the final extra-specific responsive/console/network/screenshot row while keeping CI, Aikido, deployment/rollback, and production observation separate.
- Responsive browser acceptance passed 1/1 against the Docker production web. Step 3 and Step 4 were captured at 1440x1000, 834x1112, and 390x844; all viewports passed horizontal-overflow and accessible icon-action assertions. Captured application API responses stayed below HTTP 400, catalog/quote responses retained `Cache-Control: no-store`, and no unexpected console error occurred. The known local-only missing Google Analytics key message is isolated in the evidence rather than hidden. Six screenshots and a request-body/query/cookie-free network ledger are stored at `docs/test-evidence/2026-07-11-reservation-extra-options-responsive/`. Section 6.6 is now locally complete at 8/8; CI, Aikido, deployment/rollback, and production observation remain open.
- PR #386 first review follow-up maps invalid legacy quantities to HTTP 400 on paid and unpaid endpoints, preserves percentage/fixed campaign semantics after catalog-backed legacy-extra adaptation, and prevents changed selections from being automatically resubmitted after a `409`. Focused backend tests passed 34/34, the full backend unit suite passed 733/733, TypeScript passed, Step 4 Vitest passed 15/15, the rebuilt Docker production web passed the focused conflict Chromium scenario 1/1, and all CI/CodeQL/React Doctor/Docker checks passed on commit `fc71ee6`.
- PR #386 re-review follow-up extends the same invariants to the quote-backed path and persisted browser state: percentage/fixed campaigns now include generic catalog extras before discounting; repeated legacy URL codes aggregate into one bounded quantity; Step 3 reconciles stored selections with the fresh catalog; and Step 4 stops for explicit review when option version, unit price, pricing mode, or authoritative final total changes. The repeated physical-`xmin` comment remains a false positive because Npgsql-generated SQL and the applied Docker schema omit a user-defined `xmin` column. Focused quote tests passed 4/4, full backend unit tests passed 734/734, focused Step 3/4 Vitest passed 22/22, full Vitest passed 60 files / 278 tests, TypeScript and focused ESLint passed, rebuilt API/web production images completed, both services returned HTTP 200, and the focused Docker Chromium conflict scenario passed 1/1. Aikido, deployment/rollback, and production observation remain open.

## 8. Automated Validation Matrix

Run from the repository root unless a command specifies otherwise:

```powershell
dotnet restore backend/RentACar.sln --configfile backend/NuGet.Config
dotnet build backend/RentACar.sln --no-restore
dotnet test backend/RentACar.sln --no-build
corepack pnpm -C frontend lint
corepack pnpm -C frontend exec tsc --noEmit
corepack pnpm -C frontend test
corepack pnpm -C frontend build
git diff --check
```

Run focused tests after each phase; run the complete matrix after all phases. Do not report the full solution test as passed if an environment-specific project fails. Separate product-test results from sandbox/host limitations.

Minimum backend coverage areas:

- catalog service and controllers;
- pricing controller/service;
- reservation service and controllers;
- EF migration/model/repository behavior;
- full-pricing snapshot serialization/readback and legacy fallback;
- Redis quote expiry, session binding, consumption, and replay behavior;
- authorization, audit, `xmin` concurrency, hard-delete race, and idempotency regressions;
- fresh-database and existing-database seed/backfill paths;
- legacy-use structured event coverage.

Minimum frontend coverage areas:

- admin API/hooks/page/editor;
- booking store;
- Step 3 and Step 4;
- reservation detail;
- public API client and price breakdown rendering.

## 9. Docker Desktop Browser Validation

Use the local Docker stack and real browser, not `pnpm dev`, as the acceptance target. Add a dedicated `6.6 Reservation Extra Options Validation Gate` to [13_Local_Docker_Browser_Test_Checklist.md](13_Local_Docker_Browser_Test_Checklist.md) rather than recording evidence only in this document.

Required scenario:

1. Sign in as Admin and create a draft option.
2. Confirm activation is blocked while a locale or vehicle group is missing.
3. Complete five locales, assign one vehicle group, set per-day price and maximum quantity, and activate.
4. Confirm the option appears in that group and not in another group across all five public locales.
5. Add a per-rental option and select quantities for both.
6. Confirm Step 3 display, Step 4 server quote, reservation total, and payment/request payload agree.
7. Open the reservation in admin and verify selected-extra rows and the complete pricing snapshot without double counting.
8. Issue a quote, change only the catalog price, and confirm the unexpired quote retains the promised price.
9. Issue another quote, then deactivate/archive or remove the group assignment and confirm submission returns recoverable `409` without losing customer data.
10. Restore the archived option and confirm it returns as inactive draft and requires activation validation.
11. Verify expired and cross-session quote IDs cannot create a reservation, and repeat submission cannot create a duplicate.
12. Confirm the old reservation remains unchanged and a new booking no longer shows an inactive option.
13. Confirm no unexpected console errors, failed API calls, hydration warnings, horizontal overflow, or inaccessible icon-only actions on desktop, tablet, and mobile.

Capture screenshots and concise network evidence under a new dated folder in `docs/test-evidence/`. Do not include customer PII, tokens, cookies, card data, or local secrets.

## 10. Security and Aikido Gate

Before calling implementation complete:

- verify `AdminOnly` on every admin endpoint;
- verify public inputs never carry authoritative price/content fields;
- verify Redis quote values contain no PII and are bound to a one-way session identifier;
- verify quote and reservation paths reject expiry, replay, cross-session use, and invalid availability;
- verify catalog and quote endpoints return `Cache-Control: no-store`;
- verify audit entries omit localized bodies and PII;
- verify delete behavior preserves referenced snapshots;
- verify request logs do not expose customer/payment values;
- verify no new dependency or secret was introduced.

Per `AGENTS.md`, run `aikido_full_scan` with the full content of every added or modified first-party code/config/test file. If any issue is returned, fix it and rescan until zero unresolved issues remain.

If Aikido MCP is unavailable or tenant policy blocks the scan:

1. Do not claim the Aikido gate passed.
2. Record the exact blocker without exposing repository content or credentials.
3. Use https://help.aikido.dev/ide-plugins/aikido-mcp for setup guidance.
4. Keep release readiness open until an authorized scan succeeds.

Documentation-only files do not require an Aikido code scan, but all implementation files do.

## 11. Deployment, Rollback, and Observability

Deployment order:

1. Apply the additive migration and deploy the backward-compatible backend.
2. Run migration/seed, legacy request, quote-store, reservation-create, and reservation-read smoke checks.
3. Deploy the admin frontend.
4. Deploy the public frontend.
5. Observe quote failures and legacy usage before closing rollout.

Frontend rollback is supported. After the first new-format reservation is written, do not run the down migration or deploy a backend that cannot read the new snapshot fields. Use forward-fix or a verified backup/restore procedure.

Emit structured events for quote create/expire/invalidate/consume/replay/session rejection, catalog failures, legacy adapter use, unknown legacy codes, reservation-with-extras creation, archive/restore, delete conflict, and concurrency conflict. Events include correlation-safe identifiers and option IDs/codes only. They must not include customer PII, card data, cookies, raw quote values, or authentication tokens.

The legacy adapter cleanup gate is 14 consecutive production days with zero legacy request events. Its removal is a separate release and must include deleting the retired constants, fields, tests, and documentation together.

## 12. Documentation and Status Sync

After implementation and evidence collection:

- update [13_Local_Docker_Browser_Test_Checklist.md](13_Local_Docker_Browser_Test_Checklist.md) with the new admin/public booking gate and evidence paths;
- update [10_Execution_Tracking.md](10_Execution_Tracking.md) with truthful automated, Docker/browser, and Aikido status;
- update this document's `Implementation status` only after the corresponding gates are complete;
- keep local success, CI success, Docker/browser proof, and security-tool status separate;
- do not overwrite prior evidence or mark unchecked scenarios complete.

## 13. Final Completion Checklist

- [x] Domain model, configuration, migration, and seed implemented.
- [x] Admin catalog service/API implemented with authorization, audit, and concurrency.
- [x] Public catalog and generic quote endpoint implemented.
- [x] Redis quote lifecycle, session binding, expiry, and replay protection implemented.
- [x] Selected-extra and complete pricing snapshots implemented without double counting.
- [ ] Legacy adapter, usage events, and 14-day cleanup gate implemented and documented.
- [x] Admin settings page and editor implemented.
- [x] Archive restore and concurrent hard-delete behavior implemented.
- [x] Public Step 3/Step 4 moved off hard-coded and URL-based extras.
- [x] Admin/customer reservation responses expose snapshots.
- [x] Focused and full backend/frontend validation passed or blockers recorded precisely.
- [x] Docker Desktop browser scenario completed with evidence.
- [ ] Backend-first deployment and frontend rollback smoke completed.
- [ ] Aikido full-content scan passed with zero unresolved findings, or release blocker recorded.
- [x] Checklist and execution tracking synchronized through the completed Phase 5 boundary and current partial release evidence.
- [x] `git diff --check` passed for the Phase 5 closeout diff.
- [x] Final Phase 5 commit scope contains no unrelated user changes.

Implementation is complete only when every applicable item above is closed. A green build without Docker/browser evidence, historical snapshot proof, and the required security gate is not sufficient.
