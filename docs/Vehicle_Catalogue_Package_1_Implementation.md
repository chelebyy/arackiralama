# Package 1: real vehicle catalogue

## Second PR 446 review fixes — 2026-09-25

API integration validation also passed: 54/54 tests using the retained local PostgreSQL/Redis containers with explicit `127.0.0.1` addresses. Initial `localhost` runs stalled and were stopped/timed out; the IPv4 retry completed in 48 seconds. This is an environment workaround, not a proven DNS root-cause diagnosis.

- `RentalCalendar` applies the same UTC+03:00 Turkey rental calendar as the public form. Pricing rule selection, rental days, weekday/weekend totals and campaign eligibility use local dates across FleetService, PricingService, ReservationQuoteService and the legacy PricingController path. Availability overlap instants and stored timestamps stay UTC; no historical data rewrite is performed.
- The detail form remounts each office select when its resolved URL office ID becomes available. Subsequent option refreshes with unchanged IDs preserve the visitor's selection.
- Legacy root-relative photo paths such as `/photos/1.jpg` retain their original frontend origin. Uploaded vehicle paths still use the API origin. Protocol-relative paths, backslashes, traversal, invalid escapes and control characters remain rejected.
- Validation: 823 backend tests and 298 frontend tests passed; production webpack build/TypeScript passed; lint zero errors with the existing SearchForm warning. New tests cover both sides of 03:00, pricing start dates, weekend totals, quote campaign eligibility, delayed offices and legacy media safety.
- Local real API quote: October 10 01:00 to October 13 09:00 Turkey time returned HTTP 200, three rental days, daily rate TRY 1200 and base/final total TRY 3600. No reservation or payment was submitted.
- Browser check paused the office request until the vehicle form rendered with empty selects. Releasing the request selected Gazipasa Airport and Alanya Merkez automatically. Changing pickup to Alanya and submitting the form showed the TRY 1200 daily group rate. Interception was cleared and the temporary tab closed.
- Header/Hero remain untouched. New-head CI must be checked after publication; no merge or deployment.

## PR 446 review fixes — 2026-09-25

- Addressed all three P2 findings: office code matching with Turkish/ASCII legacy name fallback; shared Turkey wall-clock to UTC conversion for catalogue, booking availability, quotes and reservation submission; accessible image failure fallback on cards and detail galleries.
- Regression evidence: 67 frontend test files / 297 tests passed, production webpack build and TypeScript passed, ESLint reported zero errors and the existing SearchForm test warning. Backend code is unchanged by this follow-up.
- Local production-build browser check against the retained PostgreSQL/Redis/API fixtures: `gzp` selected Gazipasa Airport and issued availability requests. Catalogue and booking step 2 both sent pickup `2026-10-09T22:00:00.000Z` and return `2026-10-11T22:00:00.000Z` for local October 10/12 at 01:00. This fixture had no available vehicles at GZP; no positive availability claim is made.
- Temporarily blocked a gallery JPEG in the browser: card and detail showed the car fallback; moving to the second image loaded successfully. Browser blocking and cache overrides were removed. Quote and submission timestamp equality is covered by component regression tests, not a live checkout/payment.
- Header/Hero/SearchForm source remained unchanged. No merge or deployment. Remote checks must be assessed at the new PR head; earlier green checks do not prove the follow-up.

Date: 2026-09-24. Scope: local implementation and acceptance; no deployment.

## Git and preservation

- Remote default was resolved as origin/main and fetched before implementation. Starting commit and local main: `9c777158e6188e99594f25b84ec49edf641547c3`.
- Work is isolated on `codex/catalog-package1` in `.worktrees/catalog-package1`.
- The primary checkout remains on `codex/docs-db-ops-phase0-doc-contract` at `e81eb08d07e33cfcf2979566604f82f72e56034f`. Its untracked roadmap and worktree directory were preserved.
- Existing worktrees were audited; none removed because activity, ignored-file or merge-evidence guards required preservation. No branch was deleted.
- September 25: implementation committed as `d241168db769076c63df89dc6d6394f90dd27c5d`, pushed and published in [PR #446](https://github.com/chelebyy/arackiralama/pull/446), targeting main. Initial remote checks were pending/running; local evidence below is not a claim of remote CI success. No merge, production migration or deployment is authorized. See [session handoff](handoffs/2026-09-25-catalog-package1-pr-handoff.md) for continuation.

## Delivered behavior

Vehicles now have nullable transmission, fuel, seats, approximate suitcase capacity, body, doors, engine and power fields, with verified equipment and ordered gallery arrays. Missing specifications stay null. Admin editing preserves model/year, existing photos and reserved/rented operational status.

The admin form supports multiple uploads, cover reordering and gallery-reference removal. A partially completed upload retains the saved vehicle and successful images; retry does not create another vehicle. Validation restricts codes and numeric ranges.

Public DTOs explicitly select customer-facing fields and omit plate, internal notes and maintenance data. Admin responses retain operational fields. Homepage cards, list and detail share facts and media helpers. Homepage retains four cards. Catalogue ratings/review counts, mileage promises and unverified equipment defaults were removed.

Undated browsing does not invent dates, query availability or return a daily price. Detail requires dates, times and offices before checking availability; invalid/past/reversed dates are rejected. Detail links preserve only the six search parameters, excluding arbitrary incoming price values.

Dated group indications use the pickup date's pricing rule. Missing/zero rates do not expose a booking link. The UI explains that this is neither exact-vehicle allocation nor a final total. Verified equipment is distinct from paid extras. All five locales are included.

Public Header, Hero, SearchForm, homepage composition and global styles have an empty diff.

## Migration and media safety

`20260924181928_VehicleCatalogue` adds nullable scalar columns and empty arrays. Its Up method does not drop or rewrite existing vehicles, reservations, prices, groups, model/year or photo URLs. It was applied to isolated PostgreSQL; unknown fields and legacy-photo fallback are regression-tested.

The first gallery item remains the `photoUrl` cover. Empty gallery arrays fall back to the old single photo. Gallery updates accept only a unique subset/reordering of the same vehicle's current URLs, at most twelve items.

Uploads require admin authorization, matching JPEG/PNG/WebP extension and MIME, a nonempty body of at most 5 MiB, and file signature/structure checks. Filenames are generated by the server. A scoped physical file provider serves new uploads even when their directory did not exist at startup. Runtime uploads are ignored by Git.

Reference removal and vehicle deletion do not physically delete images, preventing damage to shared references. Orphan-file reclamation is deferred. Validation is not a full image decoder or antivirus scan.

## Automated evidence

| Check | Result |
| --- | --- |
| Backend unit/service/controller suite | 818 passed |
| PostgreSQL/Redis API integration suite after upload-serving fix | 54 passed |
| FleetService regression after final pickup-date pricing change | 25 passed |
| Frontend full suite | 65 files / 290 tests passed |
| Final admin status/legacy-photo and detail regressions | 2 files / 6 tests passed, including 2 additional admin tests |
| Final Next.js production build and TypeScript | Passed |
| Frontend lint | 0 errors; existing unused-disable warning in SearchForm.test.tsx |
| Git whitespace check | Passed |

The new HTTP integration case tests unauthorized upload, disguised-image rejection, valid upload, public image bytes/content type, plate omission and null undated price. Service tests cover another vehicle remaining unchanged, legacy/shared photos, foreign/duplicate gallery references and append behavior.

The first full API run lacked services on default ports; it passed with explicit isolated PostgreSQL/Redis on 5548/6388. Dependencies were reused via a junction to retained `local-validation-20260923/frontend/node_modules` after verifying identical manifests and lockfiles. An incomplete install was preserved in `.validation/node_modules`. The junction is a retained-worktree dependency, not a self-contained install.

Build tools reported existing multiple-lockfile/middleware-convention warnings; an earlier cold build also reported Next Edge runtime warnings. No framework upgrade is included.

## Local browser evidence

Chromium through Codex computer-use; API 5000, catalogue frontend 3108, baseline 3107:

- Five synthetic vehicles were created in an isolated database. Admin changed one to automatic/hybrid/seven seats. List/detail reflected these values; another vehicle remained unspecified.
- Two valid JPEG fixtures were uploaded through the file picker, reordered, reopened in admin and viewed through public gallery navigation.
- Disguised SVG files named PNG were rejected. The saved vehicle survived; replacement JPEG upload succeeded. Errors include the server reason.
- A clean-start upload 404 was found and fixed. Public image rendering and byte-serving integration checks then passed.
- All five locale lists and details rendered translated facts and unknown states. Arabic computed direction was RTL.
- Desktop 1440x1000 and phone 390x844 layouts were checked. Measured document width equalled scroll width on tested catalogue pages.
- Homepage displayed exactly four cards; a card click retained dates, times and office context.
- Undated detail had empty date/time fields and no price/booking claim. Dated detail showed the synthetic 1200 TRY pickup-date group indication. Submitting the form retained values and resolved office IDs.
- Header/hero screenshots were visually compared at desktop and phone dimensions. The protected-file diff was empty. No pixel-difference metric is claimed.

Images are synthetic fixtures, not operator vehicle photographs. Screenshots were inspected in the session. These checks do not establish production, payment-provider or real-inventory acceptance.

At closure, the two frontend servers, API and the two catalogue test containers were stopped. No listeners remained on 3107, 3108, 5000, 5548 or 6388. Container data and synthetic uploaded files were retained; no user data was deleted. Temporary viewport overrides were reset.

## Boundaries and follow-up

Package 2 still owns removal of mandatory group-based pricing/reservation management. This package preserves group tables and the transitional booking contract. It does not certify the complete rental flow or production readiness.

Focused checks covered public/admin data separation, upload authorization/bounds, safe gallery references and rendering. External integrations, all authorization paths, image decoding, storage lifecycle and production configuration were not comprehensively reviewed. A separate focused security review and pre-release test plan were offered under the advisory Sentinel policy.
