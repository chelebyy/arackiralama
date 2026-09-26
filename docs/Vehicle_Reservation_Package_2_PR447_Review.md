# PR 447 review corrections — September 26, 2026

This record covers four correction passes on [PR #447](https://github.com/chelebyy/arackiralama/pull/447). No merge, production migration or deployment is included.

## Fourth review: starting head b8157a8563c2ecc4939daf21907c57103ea08de7

Codex r4112401035 correctly identified that admin reassignment could replace a publicly promised exact vehicle without a newly accepted quote. Assignment now rejects a different vehicle for reservations marked by schema-2 replay proof or stored booking conditions. Removal is also rejected to close the related unassign path. Both admin endpoints return HTTP 409. Selecting the existing promised vehicle is a no-op; disagreement with the proof is rejected. Legacy/manual assignment behavior remains unchanged.

### Fourth-pass validation and security coverage

- All 864 backend unit tests passed. Seven new cases cover either/both exact-contract markers, assignment/removal rejection without saving, and a same-vehicle request whose stored vehicle disagrees with the proof.
- All 47 real PostgreSQL/Redis API tests passed. Two new scenarios cover draft and confirmed pay-at-pickup exact bookings: substitute assignment and removal return 409, identical assignment succeeds without changing UpdatedAt, the vehicle/quote/snapshot/proof remain intact, and the original request still replays successfully. Existing manual assignment boundary tests also pass.
- Backend compilation, lint and whitespace checks passed. Lint retains the existing SearchForm.test.tsx warning. No frontend source/build/browser rerun or external notification/payment delivery.
- Reviewed: admin assignment/removal entry points, server-side exact-contract guards, HTTP error mapping, persisted proof/snapshot consistency, and existing hold allocation guards. The planning/source review retained authorization and public DTO boundaries; no additional material concern was found in this scoped pass.
- Not reviewed: unrelated security paths, production data, physical devices, provider delivery and deployment/migration/restore. This is a bounded source/test review, not a security certification.
- Local and fetched remote main remain `f1c34fe`; the active PR worktree continues from `b8157a8`, preserving the dirty primary checkout. Ten checks and React Doctor passed on that starting head, with GHCR publication skipped. New-head CI/review must be checked separately.

## Third review: starting head 1e4a69235df271de2b08577a3a8903f97f1e1473

| Review comment | Correction and evidence |
|---|---|
| Codex r4112314749 | Vehicle reassignment prechecks the existing occupied-until time. Real authenticated API tests reject a target booking one minute inside the preparation tail with HTTP 400 and unchanged assignment, while allowing the exact boundary. |
| Codex r4112314753 | Manual itinerary changes use vehicle-owned pricing when terms exist, including group-less vehicles. Notes/contact-only edits preserve the accepted total and snapshot without requiring a new tariff calculation. Group-based legacy pricing remains available for vehicles without their own terms. |
| Codex r4112314755 | Exact pay-at-pickup confirmation queues confirmation and future pickup/return reminders through the existing notification service. Its database queue writes participate in the same transaction as the reservation, becoming visible only after commit. Replay returns the existing reservation without duplicate jobs. Legacy unpaid requests do not queue confirmation jobs. |
| Codex r4112314757 | Manual vehicle-priced reservations persist their pricing/deposit snapshot, including zero deposits and administrator-overridden totals. Later vehicle deposit edits cannot change the accepted deposit; manual itinerary repricing preserves that deposit. No quote ID or public booking-condition lock is added to manual reservations. |

### Third-pass validation and security coverage

- All 857 backend unit tests and 45 real PostgreSQL/Redis API tests passed. Thirteen new API cases cover reassignment boundaries; grouped/group-less manual reservations with automatic/overridden totals and zero/nonzero deposits; notification creation/replay; and a forced queue write failure followed by successful retry.
- The queue-failure test injects a constraint only in the isolated test database. The reservation and an earlier queued email both roll back, the quote can be retried, and the retry stores one reservation and six queue jobs. No email/SMS delivery worker or external provider was invoked.
- Existing payment unit tests verify accepted snapshot deposit precedence, including zero, against a fake provider. The new manual API matrix verifies snapshot creation and preservation after notes and date updates.
- Backend compilation and frontend lint passed; lint retains the existing SearchForm.test.tsx unused-disable warning. No frontend source changed, and no browser or frontend production build was rerun.
- Reviewed: changed reservation paths, transaction/queue context sharing, replay, deposit precedence, and existing admin endpoint authorization. Planning retained atomic queue persistence and immutable deposits; source/diff review found no additional material issue in this scope.
- Not reviewed: production notification delivery, payment-provider behavior, unrelated authorization paths, production migrations/restore, devices or full accessibility. Queue persistence is not proof of email/SMS delivery.
- Tools run: source/diff review, EF Core transaction documentation, unit/API tests, lint and whitespace checks. Local and fetched remote main remain `f1c34fe`; the dirty primary checkout is preserved. Ten checks passed and GHCR publication was skipped on the starting head; new-head CI/review must be assessed separately.

## Second review: starting head efefb2120d6f454241fcd36ba36ad2051484d86a

The three new P2 claims were confirmed against source and corrected:

| Review comment | Correction and evidence |
|---|---|
| Codex r4112184776 | Dated catalogue loads the itinerary offices once, filters office/status/overlapping reservations in one vehicle query and eagerly loads group metadata. Rental terms and prices are calculated in memory using the same rule selection and amount calculation as individual quotes. The public DTO allowlist remains in place. PostgreSQL command interception measured exactly two read commands for both 1 and 50 vehicles. Grouped and group-less catalogue prices match individual quotes, including airport, one-way and young-driver fees. This measures query count, not production latency. |
| Codex r4112184778 | Reservation update calculates the shifted occupied-until value before the overlap precheck and saves that same value. A real authenticated API test rejects a one-minute preparation overlap with HTTP 400 and leaves the stored dates unchanged. The exact end boundary succeeds and preserves the preparation offset. |
| Codex r4112184780 | Exact quote calculation requires DriverAge before database work or quote creation. API tests assert missing age returns HTTP 400, underage returns HTTP 409, minimum eligible age succeeds and legacy group quotes still accept omitted age. Catalogue browsing can still omit age. |

### Second-pass validation

- All 857 backend unit tests passed, including legacy pricing and reservation behavior; the initial focused subset of 180 also passed.
- All 32 ReservationQuoteEndpointTests passed against isolated temporary PostgreSQL databases and Redis. Fifteen new cases cover the findings plus missing terms/rates/policies, minimum age, catalogue browsing without age, and preparation overlap/boundary filtering.
- Backend source and API tests compiled successfully. Frontend lint passed with zero errors and the pre-existing SearchForm.test.tsx unused-disable warning.
- No frontend source changed; production frontend build/browser/device acceptance was not rerun. Earlier evidence below remains historical.
- Initial compile exposed an entity/DTO mismatch, corrected by reusing FleetService's existing mapping. The first integration launch omitted Docker's implicit postgres username; the fixture connection was corrected before the successful runs. Context-mode command execution returned EPERM; scoped native tools were used.
- The fetched remote default and local main both remained `f1c34fecde4b0b9a79ffa19201d744d1be33c6ba`; corrections continue on the existing PR feature branch, preserving the dirty primary checkout.

### Second-pass scoped security coverage

- Reviewed: request validation, public DTO mapping, office and stock-blocking predicates, preparation interval preservation, and shared pricing. Planning checks required retaining the public allowlist and authoritative booking revalidation; the source/diff pass found no additional material concern in this bounded change.
- Not reviewed: unrelated auth/payment paths, real payment providers, production performance, deployment/migration/restore, physical devices or full accessibility.
- Assumptions: catalogue availability remains advisory until the existing transaction-time quote/allocation checks succeed; automated successful checks are not a security certification.
- Tools run: source/diff inspection, .NET unit and real API tests, PostgreSQL command interception, lint and whitespace checks. CI and external reviews must be checked against the newly pushed commit; React Doctor and ten CI checks passed on the preceding `efefb21` head.

## First review: starting head caa234f9142313ebee01d082a748201fa02a6208

The first pass addressed nine React Doctor and Codex inline findings.

## Verified findings and corrections

| Review comments | Correction |
|---|---|
| Codex r4112025979 | Exact-vehicle finalization now requires a driver licence expiry date server-side. Omitting Driver or its expiry field no longer bypasses eligibility. Expiry must cover the Turkey-local return date, including a return after UTC midnight conversion. Legacy group-only input remains compatible. |
| Codex r4112025981 | Exact pay-at-pickup checkout and finalization no longer depend on the legacy EnableUnpaidReservationRequest flag. Office policy and authoritative quote checks still apply. Legacy unpaid requests remain disabled when their flag is off; online payment flags remain respected. |
| Codex r4112025983 | Deposit preauthorization reads the accepted reservation pricing snapshot first, including an explicitly accepted zero deposit. Historical reservations without a snapshot retain the existing group lookup. Later vehicle/group edits cannot change an accepted deposit. |
| React Doctor r4112012135, r4112012138, r4112012141, r4112012149 | A shared CurrencyAmount component memoizes Intl.NumberFormat by locale and currency for step 2, step 4, vehicle detail and catalogue cards. Amount changes use the retained formatter. |
| React Doctor r4112012145 | Editable operating windows have stable local row IDs, preserved on field edits and sibling removal. IDs are stripped from emitted operating policy values and never become backend policy data. |
| React Doctor r4112012147 | Selected and listed extra IDs use Set membership instead of repeated array scans; selected extras outside the loaded page remain removable. |

## Validation on the corrected source

- 136 focused backend unit tests passed: PaymentServiceTests, ReservationServiceTests and ReservationServiceQuotePersistenceTests. Four new deposit cases cover grouped/group-less vehicles and nonzero/zero accepted snapshots.
- 17 real PostgreSQL/Redis API tests passed in ReservationQuoteEndpointTests. New cases cover missing expiry, absent Driver, expired dates, the Turkey-local return-day boundary, retry after rejected input, and exact pay-at-pickup with both online payment and legacy unpaid disabled. Legacy unpaid rejection and zero payment intents are asserted.
- Five frontend files / 50 tests passed under America/Los_Angeles. New cases cover exact checkout with disabled or unavailable legacy settings and DOM/value preservation when an operating window is edited and another row is removed. The five editor tests passed again after correcting their TypeScript matcher.
- Production frontend build and TypeScript passed. Lint: zero errors and the existing unused-disable warning in SearchForm.test.tsx. Existing backend obsolete-Redis-constructor warnings remain unrelated.
- Whitespace/diff checks passed. Header/Hero files were unchanged.
- This follow-up did not rerun browser acceptance; earlier Chromium evidence is retained in the [acceptance record](Vehicle_Reservation_Package_2_Acceptance.md). No external payment provider was called; deposit tests use a fake provider.
- Initial test attempts exposed an inaccessible internal calendar helper, an unsupported Testing Library matcher option and an absent feature-flag fixture. Test expectations/fixtures were corrected without weakening production checks.

## Scoped security review and remaining boundaries

Reviewed the changed eligibility, exact/legacy feature boundary, quote identity/session checks, replay flow and deposit snapshot precedence. The flag change does not authorize arbitrary vehicle input: exact requests still require a matching valid quote, configured office policy, server eligibility validation and transaction-time allocation. No authentication, online-provider enablement, payment authorization or rate-limit settings were broadened.

At the starting head, CodeQL, Gitleaks, Semgrep and all executed CI jobs passed; React Doctor's successful job still reported six warnings. Those results do not cover this follow-up commit. ECC Tools also posted heuristic evidence/permission comments on older heads; they are not proof that the existing scanners were absent, and no app permissions were changed. Assess fresh CI and review against the follow-up head.

Production settings, representative migration/restore, physical-device and full accessibility acceptance, unrelated security paths and real provider behavior remain outside this review. Original accepted snapshots and the separate activation requirements remain in force.
