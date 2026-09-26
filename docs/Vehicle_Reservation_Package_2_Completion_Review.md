# Package 2 completion review

Latest PR #447 follow-up: the sixth Codex review on `4487d98` is addressed. Active snapshotless reservations retain their vehicle group; step 3 restores exact-vehicle identity from the URL; rate priority accepts integer steps. The failed CI job passed tests but failed the Turbopack Roboto font build. The standard production build now uses supported Webpack. Validation: 875 backend unit tests, 56 frontend tests, three real PostgreSQL/Redis vehicle-catalogue API tests, build and TypeScript passed; lint has one existing warning. See [review corrections](Vehicle_Reservation_Package_2_PR447_Review.md); new-head CI must be checked after push. No browser/device rerun, merge or deployment occurred.

September 26, 2026. Scoped source, automated and real local browser review completed. The [acceptance record](Vehicle_Reservation_Package_2_Acceptance.md) closes the mobile/RTL and recovery matrix after the initial [browser retry](Vehicle_Reservation_Package_2_Browser_Retry.md). Production activation is separate.

## Planning security gap analysis

The main gaps were exact-vehicle identity across retries, lack of mandatory operating policies, group-level pricing, cache-only coordination risk and conflation of unpaid requests with confirmed contracts. The accepted design uses vehicle-owned terms, explicit admin office policy, quote fingerprints, transaction-time revalidation and PostgreSQL occupancy protection.

Unknown live operating values must fail closed. Historical snapshots must not be repriced or bulk-confirmed. Only synthetic tests use sample policies; the browser fixture uses zero notice, Saturday pickup / Tuesday return 09:00–18:00 and 60-minute preparation.

## Reviewed implementation boundaries

| Area | Controls inspected and tested |
|---|---|
| Exact identity and replay | Vehicle-bound schema 2 proof, owner/session validation, changed-payload rejection and no sibling fallback |
| Quote freshness | Current terms, selected-extra versions and policies recomputed during finalization; stale offers return conflict |
| Inventory concurrency | Referenced vehicle/office/extra/campaign row locks; PostgreSQL exclusion through accepted preparation; maintenance/office-change guards |
| Admin configuration | Existing authenticated admin routes, input bounds, mandatory policy fields, anonymous update rejection |
| Public data | Existing allowlisted catalogue/summary contracts; plate/internal policy payload omitted |
| Unpaid confirmation | Confirmed/no unpaid expiry/no payment intent; legacy callers and cleanup behavior retained |
| Migration/history | Deterministic group copy, missing office values left unset, historical snapshot/amount/status unchanged |

Locks are limited to records referenced by the booking. They do not lock all campaigns or extras. Existing reservation snapshots determine occupied-until for accepted exact bookings, so later policy edits cannot shorten earlier preparation blocks.

## Corrections and regression evidence

- Quote-bearing requests formerly could be served by generic idempotency response caching without re-running quote ownership. They now reach service replay validation. Same-key changed session/vehicle tests reject access.
- Exact hold paths now validate session ownership and retain the selected vehicle. Group-less draft holds also revalidate accepted policy before allocation.
- The final hold regression initially observed HTTP 400 for a changed-policy quote. The controller now returns the intended HTTP 409; both unchanged and changed-policy hold tests passed.
- New vehicle and office editors avoid invented operating defaults and preserve copied rules/extras. Unlisted existing extra selections remain removable.
- Real database tests verify one winner in a concurrent exact allocation, no sibling fallback, exact preparation boundary and maintenance rejection for an allocated vehicle.
- The final quote endpoint suite additionally verifies dated catalogue price/filtering and that a confirmed booking older than 24 hours survives legacy expiration processing.

## Tools and results

- Source/diff inspection of changed quote, reservation, policy, controller and migration paths.
- Complete backend unit suite: 853 passed.
- Complete real PostgreSQL/Redis API suite: 66 passed; final quote endpoint regression suite: 11 passed, including one additional catalogue test and expanded expiry coverage (67 distinct API tests across these runs).
- Frontend: 70 files / 332 tests passed under America/Los_Angeles, followed by 4 passing confirmation tests including one new regression (333 distinct tests). Final production build and TypeScript passed; lint zero errors and one pre-existing warning.
- Migration down/up rehearsal used only an isolated synthetic test database.
- The earlier saved-permission blocker cleared after Codex restart. Supported cua_repl verified office policy save/reopen and a real local exact-vehicle Confirmed booking with zero payment intents. Follow-up review covered numeric weekday serialization and narrow session/idempotency proxy forwarding; tests verify no cookie/authorization/forwarded-IP pass-through. Seven focused files / 42 tests passed after these fixes and quote initialization timing correction.
- Final browser review verified two-tab allocation, same-key/body replay after loss of an actual successful response, explicit changed-offer acceptance, localized conflict recovery, group-less booking, policy boundaries, five locales and mobile/RTL. Database contained three Confirmed fixtures and zero payment intents.
- Frontend follow-up review covered locale/query retention, actual licence dates, in-memory form recovery, stable retry keys, explicit offer acceptance and neutral summary lookup failures. Unknown exception text is not exposed and no personal data was added to persistent storage.
- No external scanner, production scan, load test or deployment was run.

## Assumptions and unreviewed areas

Production configuration, secrets, infrastructure, backup/restore, migration duration/locks on actual data, unrelated authentication/payment paths and mail delivery were not reviewed. The existing admin role model and office/delivery scope are retained. The browser record provides scoped Chromium viewport, keyboard and request-flow evidence; it does not establish physical-device compatibility or a complete assistive-technology audit.

Availability currently evaluates candidates individually; large-fleet throughput is unmeasured. Existing rate limiting remains in force. This review does not certify the repository as secure or production-ready.

## Offered stack-aware pre-release check plan

The concrete check plan is also recorded in document 13. Items 1–5 and 7 passed in the local production-build browser; item 6 has scoped source/API and frontend test evidence, not a browser penetration test. Item 8 remains a production release prerequisite:

1. Configure mandatory office policy and a group-less vehicle with seasonal price/conditions.
2. Select exact A while B remains free, change A's availability and confirm a recoverable conflict with preserved inputs.
3. Change price/conditions after quote; show and accept the replacement quote explicitly.
4. Retry identical confirmation and simulate response loss; persist one reservation, with no payment event.
5. Check pickup/return windows, closed dates, minimum notice and exact preparation boundary.
6. Check anonymous/unauthorized admin writes, public response fields, same-key changed-session rejection and safe error rendering.
7. Review five locales, Arabic RTL, desktop/mobile, keyboard flow and unchanged Header/Hero.
8. Before authorized production rollout, inspect real policy values, mappings and restore evidence, measure migration locking and retain accepted snapshots on rollback.

Scoped local acceptance is complete, including locale, responsive and recovery cases with separate evidence. Real operator values, representative migration/restore and authorized PR/CI/release review remain outstanding production activities.
