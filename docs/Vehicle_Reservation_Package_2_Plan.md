# Package 2: exact vehicle, pricing and automatic reservation

Updated September 26, 2026. Package 2 implementation and scoped local acceptance are complete. The authorized mobile/RTL and recovery continuation passed against the real local API/PostgreSQL/Redis; see the [acceptance record](Vehicle_Reservation_Package_2_Acceptance.md) and [implementation record](Vehicle_Reservation_Package_2_Implementation.md). Changes remain uncommitted. Production activation and release are separate and have not occurred.

## Baseline and outcome

Package 1 was merged through [PR #446](https://github.com/chelebyy/arackiralama/pull/446) at `f1c34fecde4b0b9a79ffa19201d744d1be33c6ba`. Package 2 continues from that verified default-branch commit in the existing `codex/catalogue-next-plan` worktree. Primary checkout changes are preserved.

The customer selects an actual vehicle, receives its authoritative offer and, when eligible, obtains an automatically confirmed reservation with payment at pickup. Group-less vehicle authoring, vehicle-level conditions/pricing and mandatory operator policies implement this outcome without inventing live operating values.

## Business decision register

| ID | Accepted decision | Implemented boundary |
|---|---|---|
| D1 | Preserve Turkey calendar-day pricing, minimum one day | Existing rule precedence, pickup-date seasonal pricing and rounding preserved; no new late-return fee |
| D2 | Operating hours and notice are unknown; mandatory admin settings are approved | Separate pickup/return windows, closed dates and minimum notice; absent policy blocks exact booking |
| D3 | Preparation is unknown; mandatory configurable setting is approved | Return-office preparation minutes, accepted value frozen in the reservation snapshot and database occupancy |
| D4 | Available vehicle automatically confirms; pay at pickup | Confirmed with amount due, no payment intent/paid event/unpaid expiry; clear five-locale customer wording |
| D5 | Preserve existing prices/conditions initially and make them editable by vehicle | Deterministic group-to-vehicle copy, seasonal rates, deposit, age/licence requirements and eligible extras |
| D6 | Keep existing office/delivery scope for this version | No arbitrary-address delivery or new fee assumptions |
| D7 | Changed price or conditions require another quote | Effective policy/price fingerprint revalidated inside final transaction; explicit customer acceptance |
| D8 | Preserve historical reservations and initial prices/conditions | Nullable legacy group reference, group-only compatibility, versioned exact replay proof, no historical contract rewrite |

Actual office values remain an operator activation prerequisite, not an unresolved implementation design. Guest management, cancellation/no-show UX, privacy-form simplification and email delivery belong to Package 3. No online payment provider is activated.

## Contract and invariants

- New public paths retain one exact VehicleId through catalogue/detail, booking, quote, commit, hold and replay. A sibling is never silently substituted.
- The server verifies vehicle status/location, active/configured offices, notice/windows/closed dates, availability through preparation, driver eligibility, vehicle prices and selected-extra eligibility/versions.
- UTC instants and Turkey calendar calculation remain consistent. The exact itinerary has a maximum 366-day implementation bound.
- Missing policy or rate produces an unavailable result; there is no zero-price or always-open fallback.
- Quotes bind itinerary, driver/coverage/campaign inputs, extras, vehicle identity and effective policy fingerprint. Changed inputs or conditions cannot reuse the offer.
- Finalization holds referenced row locks and PostgreSQL is the authoritative overlap boundary. Maintenance/office changes share allocation guards.
- Accepted totals, condition snapshots and occupancy duration remain immutable under later default edits.
- Identical authorized retries return the original result. Other sessions and changed vehicle IDs are rejected even with the same idempotency key.
- Confirmed/payment-at-pickup does not mean paid. Legacy unpaid requests remain subject to their original expiry; new confirmed bookings do not.
- Public DTO allowlists and existing admin authentication/role boundaries remain in force.

## Implementation slices

| Slice | Current delivery | Evidence boundary |
|---|---|---|
| S0 contract/fixtures | Vehicle-aware contracts, quote/replay versions, synthetic fixtures | Unit/API replay and tampering tests |
| S1 vehicle policies/migration | Nullable group, JSONB terms, copied rates/conditions, vehicle/office editors | Real migration rehearsal, group-less admin/API tests, component tests |
| S2 availability/occupancy | Shared policy service, dated exact catalogue, preparation-aware exclusion, maintenance guards | Policy unit, real PostgreSQL race/boundary tests |
| S3 quote/commit | Fingerprint, referenced-row locks, exact finalization, safe replay/hold | Changed-price/conditions/policy API tests and hold regression |
| S4 automatic confirmation | Confirmed/payment-at-pickup, no paid event or unpaid expiry | Real API persistence and legacy compatibility; expiry guard verification |
| S5 customer/admin integration | Exact cards/detail/steps, vehicle extras, accepted replacement offers, five-locale recovery, mandatory editors | 332-test full frontend run plus final 4-test regression (333 distinct); final build/TypeScript and real browser matrix passed |
| S6 acceptance/rollout | Automated suites, migration rehearsal, scoped security review, browser acceptance and synchronized docs | Local acceptance complete; actual production activation and authorized release remain separate |

## Acceptance matrix

| Scenario | Local evidence | Remaining boundary |
|---|---|---|
| Two vehicles in one legacy group; selected vehicle unavailable | Exact API race/tampering tests and two-tab browser conflict, dated reselection and preserved inputs | Production load unmeasured |
| Vehicle-specific prices/extras and no required group | Pricing/service tests, group-less admin/API and actual admin save/reopen plus booking | Actual inventory terms require operator review |
| Alter vehicle/session/itinerary/extras; retry after commit | Unit/API replay validation; withheld successful response then identical manual retry returned one reservation | No production outage drill |
| Concurrent overlapping reservations | Real PostgreSQL/Redis barrier test, one success | Production load characteristics unmeasured |
| Preparation/closed hours/notice/maintenance | Unit/API boundaries plus real browser office hours, closure, notice and preparation checks | Actual operator values |
| Turkey days and alternate timezone | Existing calendar tests and full frontend suite under America/Los_Angeles | Turkey-local browser dates checked; alternate browser timezone not emulated |
| Changed prices or conditions | API stale-quote rejection and real browser replacement acceptance at 3900/age 23 | Actual pricing inputs require operator review |
| Automatic confirmation/no payment/legacy expiry | Persisted Confirmed/no expiry/no payment intent, expiry guard checks | No real 24-hour production observation |
| Historical reservations and migration | Isolated down/up rehearsal preserves amount/snapshot/status; copies original terms | Sanitized production rehearsal, backup/restore and lock timing |
| Public privacy/operator boundary | DTO/API tests and scoped source review; anonymous policy mutation rejected | No general penetration test |
| Five locales, RTL, responsive, back/refresh | Five-locale summary, Arabic RTL, phone/tablet/desktop, keyboard, in-memory Back recovery and failed-summary retry passed | Physical devices and full assistive-technology audit unmeasured |

The earlier saved-permission rejection cleared after the user restarted Codex. Supported browser control completed the scoped local matrix; the acceptance record supersedes historical open items in the initial retry record.

## Migration, activation and rollback

Migration `20260926125053_VehicleRentalPolicies` copies each group's rates/conditions/eligible extras to its mapped vehicles and leaves offices unconfigured. It preserves legacy group APIs and historical contracts. Missing rates remain unbookable until an operator supplies them.

Before release: inventory mappings/unknown values, rehearse representative sanitized data, measure lock duration, verify backup/restore and run normal PR/CI review. Enter actual office policies and review migrated vehicle terms before customer activation. Production migration and deployment require separate authority.

To stop new exact bookings, deactivate the affected office using the existing admin control; retain already accepted contracts and replay records. Do not use destructive schema rollback or delete reservations. The migration Down path is test-only rehearsal evidence and is not a safe operational rollback after new contracts exist.

## Security checkpoints and remaining acceptance

The planning gap analysis and final focused review are recorded in [completion review](Vehicle_Reservation_Package_2_Completion_Review.md). Material corrected issues include generic idempotency-cache replay bypass, exact hold ownership and stale hold HTTP conflict mapping. Reviewed scope is bounded to changed booking/policy/admin/public DTO paths; deployment and unrelated authentication/payment code were not reviewed.

The stack-aware pre-release check plan is available in [local checklist](13_Local_Docker_Browser_Test_Checklist.md): actual local PostgreSQL/Redis plus production-build browser, concurrent exact booking, stale offer, same-key retry, admin authorization, five locales/Arabic RTL, responsive layouts and protected Header/Hero. Local entries are closed with automated or browser evidence explicitly identified. Production configuration, representative migration/restore, PR/CI and release authorization remain separate.

The [admin implementation record](15_Admin_UX_Refresh_Implementation.md), roadmap and handoff reflect this same status. No old Package 1 browser result or first-slice test count proves Package 2 acceptance.
