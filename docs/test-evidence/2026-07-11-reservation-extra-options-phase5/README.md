# Reservation Extra Options Phase 5 Docker Browser Evidence

**Date:** 2026-07-11
**Target:** local Docker Compose stack (`backend/docker-compose.yml`)
**Scope:** partial section 6.6 acceptance; unchecked scenarios remain open

## Stack

- Docker Engine: 29.6.1.
- `docker compose -f backend/docker-compose.yml up -d --build`: PASS.
- PostgreSQL and Redis: healthy.
- API `http://localhost:5000/health`: HTTP 200.
- Frontend `http://localhost:3001/tr`: HTTP 200.
- Frontend `/`: HTTP 307, `Location: /tr`.

## Browser Results

The bundled browser connection could not start because its generated ESM kernel called CommonJS `require`. The repository's existing Playwright 1.61 Chromium runner was therefore used against the same Docker URL.

- Booking/payment smoke: 6/6 PASS.
- Focused payment/admin proof: 6/6 PASS.
- Exact-final TypeScript: PASS. The expanded booking/payment run passed 6/7 in one batch after a single vehicle-list request took 16.1 seconds against a 15-second action timeout; the same remaining completion scenario passed 1/1 immediately when rerun alone.
- Verified selected `Çocuk Koltuğu` appeared in the authoritative Step 4 quote.
- Verified generated booking URLs did not contain `extras`.
- Verified quote validity status, terms acceptance validation, unpaid reservation creation, confirmation redirect, admin catalog load, and current no-extra/non-legacy admin reservation detail.
- Test-created unpaid holds were changed to `Cancelled` with their expiry cleared after evidence collection so the single seeded vehicle remained reusable.

## Continuation Results

- Exact-current Docker Chromium bundle: 16/16 PASS across `booking-flow.spec.ts`, `payment-flow.spec.ts`, `i18n.spec.ts`, and `mobile.spec.ts` using one worker.
- Added `frontend/e2e/tests/reservation-extra-options.spec.ts` as a self-cleaning acceptance suite; final expanded run: 6/6 PASS.
- The targeted scenario proved the incomplete draft state keeps `Kaydet ve Aktifleştir` disabled, then completed TR/EN/DE/RU/AR content, selected exactly one vehicle group, activated the option, and verified the assigned group returned the expected localized name in all five locales.
- Every public catalog response asserted HTTP 200 and `Cache-Control: no-store`; a second unassigned vehicle group did not return the option.
- The scenario deactivated and permanently deleted its unused test option in `finally`, including failure paths, so the local catalog was not left polluted.
- Expanded the spec with three route-controlled Step 3 scenarios covering the loading skeleton, per-day/per-rental quantities and calculated totals, catalog error/retry/empty behavior, supported-plus-unsupported legacy-link warning, and removal of `extras` from the generated Step 4 URL. These scenarios created no server-side data.
- The first expanded Chromium run passed 1/4: the original authoring scenario passed, while the three new scenarios failed only because currency locators omitted the rendered `.00` suffix and a generic `role=alert` locator also matched Next.js's route announcer. After narrowing those assertions, the final run passed 4/4 in 7.4 seconds with one worker.
- Added two Step 4 route-controlled scenarios. Paid mode proved the rendered extra line, final total and expiry, campaign-driven quote refresh, refreshed `quoteId` plus session/idempotency ownership, and reservation -> hold -> payment ordering. Unpaid mode proved its `quoteId` plus session/idempotency ownership and zero hold/payment-intent calls. No real provider call or server-side reservation occurred.
- The first 6-test run passed 5/6 because the unpaid radio locator expected `Çevrimiçi` while the rendered Turkish label begins with `Online`; the corrected unpaid scenario passed 1/1 and the final one-worker Chromium suite passed 6/6 in 9.3 seconds.
- Fresh gates from the source-identical locked workspace: SHA-256 match PASS, TypeScript PASS, ESLint PASS, Prettier PASS, focused `BookingStep4` Vitest 15/15 PASS, and targeted Playwright 6/6 PASS.

## Runtime Defect Found and Fixed

The first real unpaid reservation write returned 500. API logs showed Npgsql rejecting `DateTimeKind.Unspecified` for `timestamp with time zone` driver snapshot columns. `ReservationService.ApplyDriverSnapshot` now normalizes birth, license-issue, and license-expiry values to UTC.

- Focused backend regression: 1/1 PASS.
- Rebuilt API health: HTTP 200.
- Repeated real Chromium flow: PASS.

## Still Open

- Loading, retry, empty, and legacy-link warning interactions.
- Paid mode and payment ordering with online payment enabled.
- Price-only versus invalidating catalog changes and first/second `409` behavior.
- Immutable selected-extra snapshot history and explicit legacy-total-only row.
- Expired, cross-session, and replayed quote behavior in the browser.
- Desktop/tablet/mobile screenshots plus durable console/network capture.
- CI and Aikido release-security gates.
