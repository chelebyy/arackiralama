# Package 2 local acceptance — September 26, 2026

Latest PR #447 follow-up: the fifth Codex review on `59b3ef0` is addressed. Exact-vehicle campaigns are validated by the authoritative quote, including vehicles without a group; legacy group validation is retained. Applied status requires the returned campaign code, and campaign/checkout actions wait for pending quote work. Validation: 42 frontend tests, 49 real PostgreSQL/Redis API tests, Webpack production build and TypeScript passed. Lint has one existing warning. Default Turbopack remains blocked by the existing external node_modules junction. See [review corrections](Vehicle_Reservation_Package_2_PR447_Review.md); fresh CI/review must be assessed on the new head. No browser/device rerun, merge or deployment occurred.

Publication: implementation commit `22e20c8` is in [PR #447](https://github.com/chelebyy/arackiralama/pull/447). The results below are local validation receipts; remote checks and review must be assessed on the PR's latest head. No merge or production rollout has occurred.

Package 2 implementation and scoped local acceptance are complete. This record covers the authorized mobile/RTL and recovery continuation on `codex/catalogue-next-plan`, based on `f1c34fecde4b0b9a79ffa19201d744d1be33c6ba`. Tests used the production-build frontend on port 3108, API on 5000, PostgreSQL on 5548 and Redis on 6388, with isolated database `rentacar_pkg2_browser_20260926`. Payments were disabled. All bookings and operator edits below are synthetic local fixtures.

## Browser evidence

| Scenario | Observed result |
|---|---|
| Mobile and RTL | Turkish catalogue/form/checkout and Arabic detail/form/checkout/confirmation operated at phone widths. Phone checks at 360–390px and the 768px tablet summary had no horizontal document overflow; the final saved Arabic confirmation image measures 390px. Desktop admin and public views were also inspected. These are Chromium viewport checks, not physical iOS/Android certification. |
| Five locales | Language-menu navigation retained vehicle, itinerary, booking code and unpaid marker. English, German, Russian, Turkish and Arabic confirmation displayed the same server reservation, with localized confirmation and currency presentation. Arabic containers computed `direction: rtl`. |
| Keyboard interaction | Language menu, date/time fields, native selections, terms checkbox and form/confirmation actions were operated with keyboard input. This is focused keyboard evidence, not a complete assistive-technology audit. |
| Group-less vehicle | Admin removed Catalog 3's optional legacy group, saved and reopened it. The group remained absent and the daily rate remained 1200. Public detail, extras, quote and booking all worked without a group. |
| Changed offer | Catalog 2's open quote showed 3600 and minimum age 21. Admin changed daily price 1200 to 1300 and age 21 to 23. Submission did not reserve silently; checkout displayed 3900/23 and required explicit updated-offer acceptance. |
| Lost success response | The actual POST succeeded with code `DQM-4N2J-H2X`, but its response was intercepted and withheld until the client timed out. Further intercepted responses were failed/aborted. The UI displayed a Turkish retry message. After clearing interception, a manual retry used the identical body and idempotency key and returned the same code. Database: exactly one Catalog 2 booking, Confirmed, 3900, zero payment intents. |
| Competing exact bookings | Two tabs obtained separate quotes for group-less Catalog 3 for October 24–27 and submitted. One confirmed as `ZMP-2XP3-L7H`; the other displayed localized Arabic unavailability and remained in checkout. No sibling vehicle was substituted. Database: exactly one Catalog 3 booking, Confirmed, 3600. |
| Preserved form | After conflict, Back retained the synthetic driver's name, contact fields, birth date and actual licence issue/expiry dates. Personal data remains in memory only and is not restored after a full browser reload. |
| Office hours | Pickup 08:59 rejected and 09:00 accepted; return 17:59 accepted and 18:00 rejected against explicit 09:00–18:00 fixture windows. |
| Closed date and notice | A configured October 24 closure blocked the otherwise free Catalog 4. After removing that closure, 43200 minutes of notice blocked the October 24 request. Restoring zero fixture notice restored availability. |
| Preparation boundary | Catalog 2 returned October 20 at 10:00. With a temporary Tuesday pickup window, 10:59 was unavailable and 11:00 was available: accepted preparation was exactly 60 minutes. The original Saturday pickup window was restored. |
| Summary lookup failure | Rapid language switching produced an actual HTTP 429. This exposed an incorrect fallback to legacy 24-hour request copy. Exact-booking confirmation now retains the code, shows verification status and offers a retry, without claiming temporary allocation or confirmation while the server result is unknown. |

No test interception or network override is intended to remain enabled. Closed dates and notice changes were reverted; the fixture retains Saturday pickup / Tuesday return 09:00–18:00, notice 0 and preparation 60. Catalog 2 deliberately retains its tested 1300/23 terms, and Catalog 3 remains group-less for inspection. Existing Catalog 1 booking `C95-E543-WW2` remained Confirmed at 3600. The final database contained these three confirmed bookings and zero payment intents.

## Corrections and checks

- LanguageSwitcher preserves query parameters and dynamic route parameters without changing Header/Hero layout.
- Known booking errors map to translated recovery messages; unknown exception text is not rendered directly.
- Unchanged retry payloads reuse their idempotency key within the mounted checkout. Submission-in-flight protection prevents duplicate concurrent form actions.
- Changed exact-vehicle offers require acceptance, including unchanged-price condition changes. Refreshed extras remain scoped to the exact vehicle.
- Conflict errors remain visible with a dated catalogue link that removes the unavailable vehicle selection.
- Licence issue and expiry dates are required user inputs; the old hardcoded 2020/2030 dates were removed. Submitted in-memory details restore when returning to the form.
- Confirmation formats currency for the active locale and offers recovery from failed server verification.
- Full frontend run: 70 files / 332 tests passed under `America/Los_Angeles`. The final confirmation-error regression then passed all 4 tests in its file, including the added recovery case (333 distinct frontend tests across these runs).
- Final production build and TypeScript passed after the summary-error correction. Browser spot checks passed: intercepted summary lookup failure recovered to the same Confirmed booking, and the dated conflict link returned to available vehicles without retaining the unavailable selection.
- Full lint: zero errors, one pre-existing unused suppression in `SearchForm.test.tsx`. Earlier backend receipts remain 853 unit and 67 distinct real PostgreSQL/Redis API tests; backend source was unchanged in this continuation.

## Scoped review and release boundary

Reviewed changed locale links, form data, quote refresh, exact-vehicle retention, replay keys, localized errors and summary recovery. API authority remains unchanged: user-provided URL values never establish server confirmation, and the browser cannot override availability or accepted terms. No secrets or personal data were added to persistent browser storage. Real post-commit replay and concurrent booking outcomes supplement the existing changed-session/payload, anonymous admin rejection and public DTO tests.

Unreviewed: production configuration, unrelated auth/payment code, external providers, production migration locking, real notification delivery and large-fleet load. This is bounded local acceptance, not a general security certification. Before deployment, enter actual office policies, review migrated vehicle terms, rehearse representative migration/restore and complete authorized PR/CI/release review.

Committed review evidence: [Arabic mobile confirmation](evidence/package2/package2-ar-mobile.png), [lost-response recovery](evidence/package2/package2-response-loss-recovered.png), and [Arabic allocation conflict](evidence/package2/package2-ar-conflict.png). The conflict screenshot records the original detected conflict before the later dated-reselection link was added; the final link was separately checked in the browser. All images contain synthetic local fixtures. Earlier browser access recovery and first booking are documented in [the retry record](Vehicle_Reservation_Package_2_Browser_Retry.md).
