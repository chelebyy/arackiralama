# PR 447 review corrections

This record covers eight correction passes on [PR #447](https://github.com/chelebyy/arackiralama/pull/447). No merge, production migration or deployment is included.

## Eighth review: starting head faffa3a6ab0240d771c050642723b639f8cfca8f

| Finding | Correction |
|---|---|
| Codex r4114965584 | Exact holds throw ReservationQuoteConflictException with the vehicle-unavailable message when the selected vehicle is no longer a candidate or all candidates overlap. The precheck includes the reservation's preparation tail. Legacy group selection retains its existing fallback/null behavior. The existing frontend conflict path offers dated vehicle reselection and prevents payment. |
| Codex r4114965587 | VehicleUpdated audit details retain complete previous/current RentalTerms, including rates, deposit, driver requirements and allowed extra IDs. Initial configuration records a null previous value. |
| Codex r4114965591 | Manual/legacy updates load a changed return office before checking overlap and use its preparation minutes for both the precheck and persisted OccupiedUntilUtc. A missing destination retains the existing error; absent destination policy follows manual creation's zero-preparation fallback. Unchanged offices preserve the accepted interval, and exact quoted itineraries still require a new reservation. |

### Validation and scope

- 139 focused backend unit tests passed, including complete rental-term audit JSON and exact versus legacy hold behavior.
- 40 frontend tests passed across checkout and reservation hooks. The new competing-hold regression verifies dated reselection, no automatic replacement quote, no vehicle substitution and no payment intent.
- Frontend TypeScript and lint passed; lint retains one existing SearchForm.test.tsx warning. Production frontend code is unchanged, so its build/browser/device checks were not repeated.
- All 25 selected real PostgreSQL/Redis API tests passed. Two new grouped/group-less cases create competing exact drafts before either holds; only the first becomes Hold and the second receives HTTP 409 without substitution. Eight new return-office cases cover snapshot-backed manual and snapshotless legacy reservations, longer/shorter/unconfigured preparation, one-minute overlap rejection and exact-boundary success. Existing hold recovery, manual deposit preservation, reassignment occupancy and exact-identity restrictions also passed.
- Reviewed: selected-vehicle availability/occupancy checks, existing session verification and 409 mapping, destination preparation intervals, unchanged exact-quote restrictions, immutable deposit preservation and audit snapshots. Planning retained server-side overlap enforcement and explicit vehicle reselection; source/diff review found no additional material issue within this scope.
- Not reviewed: unrelated authorization paths, real payment-provider behavior, production configuration/migrations/restore, browser/device rendering or full accessibility. Tests use isolated local databases and Redis prefixes; no external provider is called.
- Tools run: source/diff inspection, focused unit/frontend tests, TypeScript, lint and whitespace checks. Existing EF JSON mapping documentation was reused; the change introduces no new framework API or schema migration.
- Starting-head CI passed all ten active checks; GHCR publication was skipped. Fetched remote default and local main remain f1c34fecde4b0b9a79ffa19201d744d1be33c6ba, while the existing PR checkout continues from faffa3a. Assess new-head CI and renewed Codex review separately.

## Seventh review: starting head 5e2bcd04f863b8e7c57efb06ef65894313a817bb

| Finding | Correction |
|---|---|
| Codex r4114857893 | The hold hook propagates HTTP 409. Checkout refreshes expired/changed offers, requires explicit acceptance, then creates a new draft with a new quote and idempotency key. No payment starts before successful hold acquisition. Non-refreshable conflicts retain the existing specific error handling. |
| Codex r4114857894 | OfficeUpdated audit details include complete Previous and Current OperatingPolicy values, covering initial configuration and subsequent changes to notice, preparation, windows and closed dates. |
| Codex r4114857896 | The step-3 Back link removes preferredVehicleId while retaining itinerary parameters, so step 2 does not automatically advance again. Initial vehicle-detail handoff remains supported. |
| Codex r4114857899 | Server-created replay proofs persist CheckoutOperation in the existing JSON column. Replay checks this value before returning an existing reservation, both with and without the Redis quote. A cross-operation request returns 409 and checkout obtains a fresh offer for explicit acceptance. Exact proofs without operation metadata fail closed; historical group-only proofs retain compatibility. No database migration is needed. |

### Validation

- 137 focused backend unit tests passed: FleetServiceTests, ReservationServiceTests and ReservationServiceQuotePersistenceTests. Audit tests compare complete policy JSON, including null previous state; replay tests cover live and expired Redis quote state.
- All 41 ReservationQuoteEndpointTests passed against isolated PostgreSQL databases and Redis. Both endpoint directions reject cross-operation replay with the original or a different idempotency key; same-operation retries still succeed and preserve stored status.
- A final three-case real API run passed after extending hold recovery coverage: unchanged policy, changed policy and expired accepted snapshot. The two conflicts leave the old draft nonblocking; a fresh quote/draft acquires the sole hold successfully. There are 42 distinct API cases across these runs.
- Four frontend files / 59 tests passed, followed by all 34 step-4 tests after adding the operation-conflict case: 60 distinct frontend tests across runs. Coverage includes hook error propagation, the Back-link destination, explicit replacement-offer acceptance, idempotency-key rotation and withholding payment until the replacement hold succeeds.
- Production build and TypeScript passed. Lint passed with zero errors and the existing SearchForm.test.tsx unused-disable warning. Existing build warnings concern lockfile-root inference, middleware deprecation and an Edge Runtime dependency.
- Initial frontend attempts required correcting test setup: Windows command-wrapper path parsing, explicit credit-card selection and sequential UUIDs for key-rotation assertions. Production guards were retained.

### Scoped security coverage and boundaries

- Reviewed: operation/session/quote replay boundaries, persisted JSON proof mapping, hold-error recovery, explicit customer acceptance and office-policy audit details. Planning required preserving session verification and immutable accepted terms, preventing cross-operation success, and avoiding automatic payment on refreshed terms. The final source/diff pass found no additional material concern within this scope.
- Not reviewed: unrelated authentication paths, real payment providers, production deployment/migration/restore, browser/device rendering and full accessibility. Earlier browser evidence is historical and does not validate this follow-up.
- Assumptions: PostgreSQL overlap enforcement and existing endpoint authorization remain authoritative; mocks verify frontend interaction, not provider behavior. Test databases are isolated local fixtures.
- Tools run: source/diff review, Context7 EF Core value-conversion documentation, focused unit/frontend tests, real PostgreSQL/Redis API tests, lint, production build/TypeScript and whitespace checks.
- Fetched remote default and local main both remain f1c34fecde4b0b9a79ffa19201d744d1be33c6ba. Work continues on the existing PR branch; the dirty primary checkout is preserved. The worktree cleanup audit skipped every candidate because of uncommitted files, unreviewed ignored content or absent exact-head merge evidence; none was removed.
- Assess CI and renewed Codex review on the newly pushed commit. Existing ECC Tools comments describe heuristic evidence/app-permission gaps; this change does not alter GitHub App permissions or claim general security certification.

## Sixth review: starting head 4487d988be36cea982ce66662924659296969051

The [repair plan](PR447_Sixth_Review_Plan.md) records the diagnosis and scope.

| Finding | Correction |
|---|---|
| Codex r4112695224 | Fleet updates reject group removal or replacement while non-terminal snapshotless reservations depend on the existing group. Rejection precedes entity/audit mutation. Same-group edits, terminal reservations and snapshot-backed reservations remain supported. |
| Codex r4112695229 | Step 3 uses the URL preferredVehicleId when no vehicle is stored, in both the extras fetch and cache key. An existing stored vehicle remains authoritative over a stale URL. |
| Codex r4112695232 | Priority uses step 1; prices and multipliers retain step 0.01. Native input validity rejects fractional priority. |
| CI job 108480924718 | Type check, lint and tests passed; the build failed with 27 Turbopack Roboto font import errors, including next/font/google queries have exactly one entry. The production build script now uses the supported next build --webpack option. Fonts, dependencies and workflow permissions are unchanged. |

### Sixth-pass validation and security coverage

- 875 backend unit tests passed, including 11 new cases covering every non-terminal reservation status, group removal/replacement, unchanged group edits, terminal reservations and accepted snapshots.
- 56 focused frontend tests passed across six files. Five new cases cover grouped/group-less URL recovery, cache-key changes, store precedence and integer-versus-decimal numeric validity.
- Three real PostgreSQL/Redis vehicle-catalogue API tests passed, including two new cases covering unauthorized mutation, HTTP 400 for an active snapshotless dependency with unchanged vehicle state, allowed removal after completion, and retained snapshot deposit for an active snapshot-backed reservation.
- Standard production build, TypeScript, lint and whitespace checks passed. Existing lint/obsolete Redis constructor warnings remain. Native terminal result retrieval stalled; no result is claimed for the initial broader API run. After verifying no corresponding validation process remained, targeted API/build/lint reruns through the working command runner returned success.
- Reviewed: authenticated fleet mutation and deposit fallback preservation, exact identity propagation to server-filtered extras, numeric API contract, and build command scope. No additional material concern was found in this bounded pass. Browser/device acceptance, unrelated security paths, provider delivery, production data and deployment were not reviewed or rerun; this is not a security certification.
- Fetched remote main and local main remain f1c34fe; the dirty primary checkout is preserved. New-head CI must be evaluated separately after push.

## Fifth review: starting head 59b3ef01cacc831470a6dc1edfb74d11cb5bf081

Codex [r4112582993](https://github.com/chelebyy/arackiralama/pull/447#discussion_r4112582993) correctly identified that the legacy campaign-validation endpoint rejects the empty group identifier used by group-less exact vehicles. Checkout now validates all exact-vehicle campaigns through the authoritative quote endpoint. The legacy group-only path retains its validation request. The UI marks a campaign applied only when the returned quote includes the requested normalized code; pending quote/campaign work blocks duplicate application and checkout submission.

### Fifth-pass validation and security coverage

- All 42 focused frontend tests passed across five files, including four new cases covering grouped/group-less exact vehicles, pending quote controls, rejected-code retry and a quote without the requested campaign. Existing legacy validation tests still pass.
- All 49 real PostgreSQL/Redis API tests passed. Two new cases prove a global 10% campaign reduces a group-less exact booking from TRY 3,000 to TRY 2,700, persists the campaign/discount in the confirmed reservation snapshot, and rejects a campaign restricted to a vehicle group with HTTP 409. Backend production logic was unchanged.
- Webpack production build, TypeScript, lint and whitespace checks passed. Lint retains the existing SearchForm.test.tsx warning. The default Turbopack build failed because the pre-existing node_modules junction points outside its inferred root; `corepack pnpm -C frontend build --webpack` passed without changing that shared dependency link or repository build settings.
- Reviewed: campaign routing, authoritative returned-code handling, pending-request controls, server campaign eligibility and persisted quote totals. Pricing/eligibility remain server-enforced; no additional material concern was found in this scoped source/test pass.
- Not rerun/reviewed: browser/device acceptance, unrelated security paths, the full backend unit suite, external notification/payment delivery, production data and deployment/migration/restore. Existing browser/unit evidence below is historical. This is not a security certification.
- Local and fetched remote main remain `f1c34fe`; the active PR worktree continues from `59b3ef0`, preserving the dirty primary checkout. Ten checks and React Doctor passed on that starting head, with GHCR publication skipped. New-head CI/review must be checked separately.

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
