# PR 447 review corrections — September 26, 2026

This follow-up addresses the nine React Doctor and Codex inline findings on [PR #447](https://github.com/chelebyy/arackiralama/pull/447). Reviewed starting head: `caa234f9142313ebee01d082a748201fa02a6208`. No merge, production migration or deployment is included.

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
