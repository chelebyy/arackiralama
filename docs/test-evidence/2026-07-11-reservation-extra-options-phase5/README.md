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
- Added `frontend/e2e/tests/reservation-extra-options.spec.ts` as a self-cleaning acceptance suite; final expanded run: 9/9 PASS.
- The targeted scenario proved the incomplete draft state keeps `Kaydet ve Aktifleştir` disabled, then completed TR/EN/DE/RU/AR content, selected exactly one vehicle group, activated the option, and verified the assigned group returned the expected localized name in all five locales.
- Every public catalog response asserted HTTP 200 and `Cache-Control: no-store`; a second unassigned vehicle group did not return the option.
- The scenario deactivated and permanently deleted its unused test option in `finally`, including failure paths, so the local catalog was not left polluted.
- Expanded the spec with three route-controlled Step 3 scenarios covering the loading skeleton, per-day/per-rental quantities and calculated totals, catalog error/retry/empty behavior, supported-plus-unsupported legacy-link warning, and removal of `extras` from the generated Step 4 URL. These scenarios created no server-side data.
- The first expanded Chromium run passed 1/4: the original authoring scenario passed, while the three new scenarios failed only because currency locators omitted the rendered `.00` suffix and a generic `role=alert` locator also matched Next.js's route announcer. After narrowing those assertions, the final run passed 4/4 in 7.4 seconds with one worker.
- Added two Step 4 route-controlled scenarios. Paid mode proved the rendered extra line, final total and expiry, campaign-driven quote refresh, refreshed `quoteId` plus session/idempotency ownership, and reservation -> hold -> payment ordering. Unpaid mode proved its `quoteId` plus session/idempotency ownership and zero hold/payment-intent calls. No real provider call or server-side reservation occurred.
- The first 6-test run passed 5/6 because the unpaid radio locator expected `Çevrimiçi` while the rendered Turkish label begins with `Online`; the corrected unpaid scenario passed 1/1 and the final one-worker Chromium suite passed 6/6 in 9.3 seconds.
- Fresh gates from the source-identical locked workspace: SHA-256 match PASS, TypeScript PASS, ESLint PASS, Prettier PASS, focused `BookingStep4` Vitest 15/15 PASS, and targeted Playwright 6/6 PASS.
- Added two catalog-change scenarios. The price-only path kept the issued unexpired quote and promised total. The availability-invalidating path refreshed catalog/quote once, retried with the original idempotency key, stopped on a second `409`, preserved debit-card/card-field/terms state, and required explicit quote refresh before a new submission.
- The first conflict characterization reached the second-`409` state but observed three quote calls instead of two. Updating the persisted selection triggered the normal automatic quote effect in parallel with the explicit recovery refresh. Step 4 now records the refreshed selection key before updating the store; the focused 2/2 rerun and complete 8/8 one-worker Chromium suite passed against the rebuilt production Docker web.
- Added a real immutable-history scenario. It created a child-seat unpaid reservation, asserted the persisted selected-extra row and raw full-pricing snapshot, recomposed the final total without double counting, changed the live child-seat Turkish name and price, and verified both admin API history and rendered admin detail remained unchanged. It then opened a real pre-migration reservation and asserted the explicit legacy-total-only warning.
- The first snapshot pass exposed that `normalizeReservation` returned the raw backend price-breakdown object unchanged when it was truthy, so `baseTotal`, `finalTotal`, and `campaignDiscount` did not reach the admin UI's normalized fields. The corrected admin client always maps the raw snapshot contract. Focused admin API Vitest passed 13/13; TypeScript, ESLint, Prettier, Docker production build, and health checks passed.
- Focused immutable-history Playwright passed 1/1. Repeated diagnostics then exhausted the local in-memory rate limiter and produced a non-product 7/9 run with HTTP 429; after restarting only the local API runtime, the clean complete reservation-extra Chromium suite passed 9/9 with one worker.
- Cleanup verification confirmed the built-in child seat returned to `Çocuk Koltuğu`, 75 TRY/day, active/non-archived, and every reservation created by the immutable-history attempts was cancelled.
- Final gates for this continuation: TypeScript PASS, focused admin API Vitest 13/13 PASS, Docker production build PASS, focused immutable-history Playwright 1/1 PASS, and complete reservation-extra Playwright 9/9 PASS.
- Validated immutable-history closure handoff: `C:\Users\muham\AppData\Local\Temp\2026-07-11-194923-reservation-extra-options-immutable-history-handoff.md` — 100/100, READY; the external temp artifact is refreshed with the actual commit SHA after commit creation.
- Validated OS-temp continuation handoff: `C:\Users\muham\AppData\Local\Temp\reservation-extra-options-catalog-conflict-handoff-2026-07-11.md` — 100/100, READY.

## Runtime Defect Found and Fixed

The first real unpaid reservation write returned 500. API logs showed Npgsql rejecting `DateTimeKind.Unspecified` for `timestamp with time zone` driver snapshot columns. `ReservationService.ApplyDriverSnapshot` now normalizes birth, license-issue, and license-expiry values to UTC.

- Focused backend regression: 1/1 PASS.
- Rebuilt API health: HTTP 200.
- Repeated real Chromium flow: PASS.

## Still Open

- Desktop/tablet/mobile screenshots plus durable console/network capture.
- CI and Aikido release-security gates.

## Quote Lifecycle Continuation

- Added a Docker-backed Chromium scenario for expired, cross-session, and replayed quote IDs.
- A real Redis quote key was expired with `PEXPIRE`; reservation submission returned `409`, and PostgreSQL reported zero rows for that `quote_id`.
- A valid quote submitted with a different `X-Session-Id` returned `409`, and PostgreSQL again reported zero rows.
- The initial browser-issued quote created one reservation. Replaying it with a different idempotency key returned the original reservation ID; PostgreSQL reported exactly one row for the unique `quote_id`.
- Cleanup cancelled the created reservation, removed quote/claim/consumed Redis keys, restored the built-in child seat to `Çocuk Koltuğu` at 75 TRY/day, and confirmed zero active quote-replay or snapshot test reservations.
- Current-source gates: TypeScript PASS; ESLint PASS; Prettier PASS; existing reservation-extra Chromium scenarios 9/9 PASS; focused quote-lifecycle Chromium 1/1 PASS; focused `ReservationQuoteEndpointTests` 1/1 PASS.
- The browser scenarios ran in two clean API windows because repeated real-service diagnostics can exhaust the local process-scoped rate limiter; failed 429/timeout runs are not claimed as product evidence.
