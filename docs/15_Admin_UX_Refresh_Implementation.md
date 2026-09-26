# Admin Public Site & Contact UX Refresh Implementation

Latest PR #447 follow-up: the second Codex review on `efefb21` is addressed with batched catalogue queries, preparation-aware reservation updates and required age for exact quotes. Validation: 857 backend unit tests and 32 real PostgreSQL/Redis API tests passed; catalogue queries stayed at two for both 1 and 50 vehicles. Lint passed with one existing warning. See [review corrections](Vehicle_Reservation_Package_2_PR447_Review.md); fresh CI/review must be assessed on the new head. Earlier frontend/browser receipts retain their original scope; this pass changes no frontend source and performs no merge or deployment.

## Current Package 2 local acceptance — September 26, 2026

Package 2 implementation and scoped local acceptance are complete. The user-authorized mobile/RTL, changed-offer, response-loss and competing-booking checks passed against the actual isolated API/PostgreSQL/Redis fixture. Production activation remains a separate release step.

Frontend: 70 files / 332 tests passed under America/Los_Angeles; the final summary-recovery change passed all 4 focused tests, including one new case (333 distinct tests across runs). Final production build/TypeScript passed. Lint: zero errors and one pre-existing warning. Earlier unchanged-backend evidence remains 853 unit and 67 distinct PostgreSQL/Redis API tests.

Browser evidence covers group-less admin save/reopen and booking, five-locale confirmation, Arabic RTL, phone/tablet/desktop views, keyboard operation, explicit acceptance of changed price/conditions, same-body/key recovery after a lost success response, one winner from two exact-booking tabs, and office hours/closed-date/notice/preparation boundaries. Licence dates are now real required inputs; in-memory details survive Back. Failed confirmation lookups retain the code and offer retry without misleading status claims. Header/Hero source and layout are unchanged.

Package 2 implementation commit `22e20c8` is published in [PR #447](https://github.com/chelebyy/arackiralama/pull/447); current-head CI and review remain pending. No merge, deployment or production migration was performed. Enter actual operator policies and review migrated vehicle terms before activation; production migration/restore checks and release authorization remain required. Local viewport checks do not certify physical devices or constitute a general security audit.

See [acceptance evidence](Vehicle_Reservation_Package_2_Acceptance.md), [implementation](Vehicle_Reservation_Package_2_Implementation.md), and [scoped security review](Vehicle_Reservation_Package_2_Completion_Review.md). Earlier entries below are historical snapshots.

The [local checklist](13_Local_Docker_Browser_Test_Checklist.md) is synchronized. Synthetic Catalog 2 retains the tested 1300 daily rate / minimum age 23; Catalog 3 remains group-less. Temporary office closures/notice/day changes were reverted.

## Historical catalogue closure — September 26, 2026

Package 1 [PR #446](https://github.com/chelebyy/arackiralama/pull/446) is merged as `f1c34fecde4b0b9a79ffa19201d744d1be33c6ba`. [Merge-commit CI](https://github.com/chelebyy/arackiralama/actions/runs/36235314898), CodeQL, Secret Scan and React Doctor succeeded; CI includes successful GHCR publication. Production deployment and migration application are unverified. Earlier open-PR/no-merge entries below are retained as historical evidence.

No admin or public source behavior changed in this documentation continuation. Previous local acceptance (827 backend / 54 API integration / 308 frontend tests) was not rerun. Full current-head E2E remains unverified; older failures belong to `9c77715`. See the synchronized [local checklist](13_Local_Docker_Browser_Test_Checklist.md) and [Package 1 closure](Vehicle_Catalogue_Package_1_Implementation.md).

The authorized next deliverable is the [Package 2 plan and decision register](Vehicle_Reservation_Package_2_Plan.md). It specifies future vehicle-level pricing, conditions and extras authoring without requiring operators to create a group. That admin work is not implemented. Public Header/Hero protection, five locales, role enforcement and existing accepted reservation snapshots remain constraints.

## Second catalogue review follow-up — 2026-09-25

API integration suite: 54/54 passed against local PostgreSQL/Redis using explicit IPv4 addresses.

The shared backend pricing calendar now uses Turkey business dates without rewriting stored UTC timestamps. Public detail office selects recover from delayed office loading, and legacy root-relative photo URLs remain supported. Admin authoring behavior is unchanged. Validation: 823 backend / 298 frontend tests passed, production build/TypeScript passed, lint zero errors and one existing warning. Real local quote and delayed-office browser evidence are recorded in the [Package 1 document](Vehicle_Catalogue_Package_1_Implementation.md#second-pr-446-review-fixes--2026-09-25). No merge or deployment.

## Catalogue PR 446 review follow-up — 2026-09-25

The catalogue review follow-up changes public office resolution, rental datetime serialization and image error fallback. Admin behavior is unchanged. All 297 frontend tests, production build and TypeScript passed; lint has zero errors and one existing warning. Local API-backed browser checks verified GZP resolution, matching catalogue/booking UTC instants and image fallback/recovery. No merge or deployment. See [Package 1 evidence](Vehicle_Catalogue_Package_1_Implementation.md#pr-446-review-fixes--2026-09-25) and the synchronized local browser checklist.

**Created:** 2026-07-08  
**Scope:** Admin usability refresh for Public Site & Contact authoring  
**Primary user:** Site owner/admin editing customer-facing content  
**Verification gate:** Focused admin browser smoke in Docker Desktop
**Implementation status:** Completed for the focused slice on 2026-07-08

## 1. Summary

### 2026-09-23 Vitest 5 follow-up

PR #444 aligns Vitest and its V8 coverage provider at 5.0.1 and regenerates the lockfile with pnpm 9.15.9 while preserving all 13 dependency overrides. On Node 24.13.0, all 296 tests across 64 files passed; coverage measured 80.43% statements, 68.36% branches, 80.06% functions, and 81.20% lines. TypeScript, production build, and lint passed, with the existing unused eslint-disable warning in SearchForm.test.tsx. This test-tool upgrade adds no Docker browser or production acceptance evidence.

### 2026-09-23 dependency review validation

The dependency follow-through aligns Tiptap packages, adapts Recharts 3 and Lucide 1 consumers, updates the Redis test wrapper, and uses Node 24 for frontend CI and Docker. Local Release backend build and 807 unit tests passed. Frontend typecheck, lint (one pre-existing warning), all 296 tests in 64 files, coverage thresholds, and production build passed. Docker Desktop was unavailable; container builds and PostgreSQL/Redis integration remain CI gates. No production deployment or browser acceptance is claimed by these checks.

This document is the implementation source of truth for making the admin
Public Site & Contact authoring experience easier to use. The desired outcome
matches the earlier improvement made to the `Icerik Yonetimi` area: the admin
should be able to understand what is being edited, which public page or contact
surface it affects, which locale is active, and whether the changes were saved
or published.

This is not a broad admin dashboard refresh. Reservations, fleet operations,
dashboard metrics, and operational summary APIs are outside this slice unless
they are required only for navigation consistency.

The admin UI should keep the current shadcn/Radix/Lucide dashboard foundation.
The customer-facing public site design is not being redesigned in this work;
the focus is the admin authoring surface that controls public content and
contact information.

2026-07-08 implementation closeout:

- Refreshed the managed page and contact authoring surfaces with explicit
  active-locale, saved/dirty, draft/published, global/local, and hidden-row
  state signals.
- Contained settings navigation overflow for narrow mobile admin viewports.
- Updated focused Public Content manager tests for the new authoring signals.
- Completed the Docker Desktop browser design validation gate documented in
  `docs/13_Local_Docker_Browser_Test_Checklist.md#65-admin-public-site--contact-ux-validation-gate`.
- Saved supporting browser evidence under
  `docs/test-evidence/local-docker-2026-07-08-admin-ux/`.
- Closed the `Microsoft.OpenApi` NU1903 / GHSA-v5pm-xwqc-g5wc dependency
  follow-up discovered during Docker rebuild by pinning patched
  `Microsoft.OpenApi` 2.7.5 in the API project and re-running backend
  vulnerability/build/test verification.
- Aikido MCP/tool was unavailable, so `aikido_full_scan` could not run; this is
  explicitly tracked as the remaining security-tooling blocker for release
  gating.

## 2. Goals

- Make `/dashboard/settings/public-content` easier to scan and operate.
- Make Public Site & Contact settings discoverable from the admin settings area.
- Clearly separate managed public page content from contact information and
  technical public-site settings.
- Preserve existing five-locale authoring where supported.
- Make draft, published, hidden, and save states explicit enough that stale or
  inactive content is not submitted accidentally.
- Improve save feedback, validation feedback, and changed-field confidence
  without changing the public route structure.
- Keep the implementation lightweight and avoid new backend/API work unless the
  existing API cannot support the required UX safely.

## 3. Non-Goals

- Do not redesign `/dashboard/default`, `/dashboard/reservations`,
  `/dashboard/reservations/[id]`, or `/dashboard/fleet/vehicles` in this slice.
- Do not implement `GET /api/admin/v1/operations/summary`.
- Do not redesign the customer-facing public site.
- Do not replace the current admin UI component foundation.
- Do not introduce saved user views, workflow engines, or analytics counters.
- Do not broaden public-content permissions or weaken existing `AdminOnly` /
  `SuperAdminOnly` boundaries.
- Do not mark Docker/browser validation complete until it has been run and
  documented.

## 4. Current Misalignment and Disposition

The earlier broad admin dashboard plan led to implementation work in unrelated
operation pages:

- `/dashboard/default`
- `/dashboard/reservations`
- `/dashboard/fleet/vehicles`
- shared admin primitives under `frontend/components/admin/ui/`
- reservation page tests tied to the operations refresh

That work is not part of the Public Site & Contact UX refresh. Before continuing
implementation, decide one of these dispositions:

- **Preferred:** park the operation-page changes for a separate future admin
  operations PR and keep this slice focused on Public Site & Contact.
- **Alternative:** revert the operation-page changes if they are not intended
  to ship soon.
- **Do not do:** mix operation-page changes with Public Site & Contact changes
  in the same PR, because review scope and acceptance gates become unclear.

`AGENTS.md` changes are also separate from this UX slice and should not be
bundled with the Public Site & Contact implementation unless there is a direct
process reason.

## 5. Implementation Phases

### Phase 1 - Read-Only UX Audit

Audit the current Public Site & Contact surfaces before editing code:

- `/dashboard/settings/public-content`
- `/dashboard/settings/system`, only for public-site/contact settings that still
  live there
- admin settings navigation/sidebar links for `Public Site & Iletisim` and
  `Icerik Yonetimi`
- affected public pages for output sanity, especially contact and managed legal
  pages

Capture:

- Which fields are public page content versus contact information.
- Which fields are locale-specific versus global.
- Which controls publish/unpublish content.
- Which fields can be hidden or inactive.
- What the admin sees during loading, save, error, and success states.

Acceptance:

- The next implementation slice has a concrete list of affected components and
  no operation-page scope.
- Any system-setting fields that still control public display are identified.

### Phase 2 - Focused Admin UX Improvements

Improve the authoring experience without changing the data model first:

- Split the page into obvious sections for managed pages, contact channels,
  offices/working hours, map/payment public display, and technical settings if
  applicable.
- Make the active locale visible and hard to confuse with global settings.
- Keep publish/unpublish/draft status near the content it affects.
- Add clear unsaved/saving/saved/error feedback.
- Use compact, repeatable row editing patterns for contact channels, offices,
  and working hours.
- Keep hidden/inactive rows visible to the admin with explicit state labels.

Acceptance:

- The admin can answer: "What am I editing?", "Which language does this affect?",
  "Is it visible on the public site?", and "Was it saved?" without extra
  investigation.
- Global contact settings are not confused with locale-specific public page
  content.
- Hidden or inactive rows are not submitted accidentally as if they were active
  content.

### Phase 3 - Preview and Confidence Improvements

After the core editing surface is clear, add lightweight confidence features:

- Readability or preview panels where feasible without changing public routes.
- Clear links to affected public pages for manual verification.
- Better validation copy for invalid URLs, empty required fields, or unsafe map
  embed values if those states already exist in the current data model.

Acceptance:

- Admins can verify the likely public impact before or immediately after save.
- Error messages stay non-sensitive and point to the editable field.

## 6. API Boundary

Prefer the existing Public Content and Public Site Settings API surfaces. This
refresh should not add a new backend endpoint unless the audit proves the
existing API cannot safely support the desired admin flow.

If API work becomes necessary, it must remain inside the existing admin
public-content/settings boundary and keep current authorization rules.

Data rules:

- Do not expose secrets, tokens, connection strings, or private operational
  metadata in the admin UI.
- Keep customer-facing text, contact rows, map/public display fields, and
  technical settings clearly separated.
- Preserve existing fallback behavior for incomplete managed records.

## 7. UI Acceptance Criteria

Every refreshed Public Site & Contact admin surface must pass these checks:

- No horizontal page overflow at desktop, tablet, or mobile test widths.
- Page content, contact rows, forms, dialogs, and preview/readability panels do
  not overlap.
- Sidebar/header do not cover page content.
- Locale-specific and global settings are visually distinct.
- Save/publish/unpublish actions show progress and completion/failure feedback.
- Empty states explain what happened and offer a next action when appropriate.
- Error states are visible and do not expose sensitive implementation details.
- Loading states avoid blank screens.
- Browser console has no material runtime errors.
- Network panel has no unexpected application `4xx` or `5xx`.

## 8. Test Strategy

Automated checks:

- Frontend Vitest/Testing Library:
  - public-content manager rendering,
  - locale tab/selector behavior,
  - page draft save and publish/unpublish behavior,
  - contact channel/office/working-hour editing,
  - loading, error, saving, and saved states,
  - no accidental local hiding of rows when API state should stay visible.
- Existing admin API client tests must be updated only if endpoint paths or
  client helpers change.
- Backend tests are required only if this slice changes backend behavior.

Manual/Browser checks:

- Docker Desktop must be running for final browser validation.
- Local Docker compose stack must be used as the design-validation target.
- Tests must run through a real browser session after admin login.
- Evidence must be recorded in `docs/13_Local_Docker_Browser_Test_Checklist.md`
  after the pass.
- 2026-07-08 result: completed against the local Docker stack with 18
  page/viewport browser checks and recorded evidence.

## 9. Docker Desktop Design Validation

Required admin pages:

- `/dashboard/settings/public-content`
- `/dashboard/settings/system` if Public Site & Contact controls remain there

Required public sanity pages, when content/contact output is changed:

- `/tr/iletisim`
- `/tr/privacy`
- `/tr/terms`

Required viewports:

- Desktop: `1440x900`
- Tablet: `768x1024`
- Mobile: `375x812`

Per-page checks:

- Page loads without blank screen.
- No horizontal overflow.
- Forms, repeated contact rows, and preview/readability areas do not break.
- Header/sidebar do not collide with content.
- Primary save/publish actions are reachable.
- Locale/global setting boundaries remain understandable.
- Console has no material runtime error.
- Network has no unexpected application `4xx` or `5xx`.

Evidence rules:

- Do not mark the Docker design gate complete from unit tests alone.
- If Docker Desktop is unavailable, document it as a blocker, not as a skipped
  success.
- If browser extensions create external network noise, identify it separately
  from application traffic.
- 2026-07-08 result: gate passed for the focused Public Site & Contact UX
  slice; see `docs/test-evidence/local-docker-2026-07-08-admin-ux/evidence.md`.

## 10. Security Requirements

- Run `aikido_full_scan` on generated, added, and modified first-party code
  after implementation.
- Provide full changed file content to Aikido when scanning code.
- Fix any reported issues in modified code and rescan until no remaining/new
  issues are reported.
- If the Aikido MCP server is unavailable, report that it must be installed
  using the official guide: https://help.aikido.dev/ide-plugins/aikido-mcp
- Do not include secrets, tokens, connection strings, or production credentials
  in docs or UI.
- Keep logs and browser-visible error messages non-sensitive.

## 11. Completion Criteria

This UX refresh is complete only when all are true:

- The scope is limited to Public Site & Contact authoring surfaces.
- Unrelated operation-page changes are parked, reverted, or moved to a separate
  PR.
- Public content and contact editing flows are easier to scan and have explicit
  save/publish/visibility feedback.
- Locale-specific and global settings are clearly separated.
- Frontend automated tests for touched public-content/contact behavior pass.
- Backend automated tests pass if backend behavior changes.
- Docker Desktop browser design validation is completed and documented for the
  targeted admin/public pages.
- Aikido scan is completed for changed first-party code, or the missing Aikido
  MCP blocker is explicitly reported.

Current status on 2026-07-08: complete for the focused admin Public Site &
Contact UX slice. The OpenAPI dependency warning found during Docker rebuild is
closed and verified; the Aikido MCP availability blocker remains explicitly
reported for release/security gating.

## September 23, 2026 local regression validation

Current main (`ce23b54`) was built in an isolated Docker stack with payments and email disabled. Desktop and mobile Chromium verified normal admin login and the admin detail of a synthetic reservation created through the public unpaid-request flow. Public confirmation and tracking displayed the same reservation code; the browser sent no payment-intent or hold request.

The tracking input lacked an accessible name. It now uses the existing localized code prompt as its accessible label, with a focused unit regression test. Tracking E2E selectors were scoped to the reservation form and its actual error element; the not-found test now asserts the displayed error. An opt-in localhost-only E2E test covers unpaid submission through admin detail. Fresh local inventory required synthetic pricing rules before a quote could be produced; this setup and commands are recorded in `13_Local_Docker_Browser_Test_Checklist.md`.

This pass does not reopen the completed Public Site & Contact redesign or establish production/payment-provider acceptance. Existing security-gating limitations above remain separate from the local browser evidence.

## September 24, 2026: vehicle catalogue Package 1

Publication update, September 25: [PR #446](https://github.com/chelebyy/arackiralama/pull/446) is open; see the [session handoff](handoffs/2026-09-25-catalog-package1-pr-handoff.md). Merge and deployment remain unauthorized.

Admin vehicle editing now supports nullable verified specifications, equipment and an ordered photo gallery. Unknown values remain unspecified; legacy photos/model/year and reserved/rented status are preserved. Successful partial uploads remain saved for retry. Only the affected vehicle changes.

The public card/list/detail share these values; admin plate remains available while public DTOs omit it. Local browser acceptance covered editing, valid/invalid uploads, ordering, five languages and RTL/mobile layouts. Header/Hero remain untouched. Backend (818 unit/service/controller, 54 API integration), frontend (290 full-suite tests plus two new admin preservation cases), final build/TypeScript and lint checks passed, with the existing lint warning documented.

See [Package 1 implementation and evidence](Vehicle_Catalogue_Package_1_Implementation.md) and the matching September 24 entry in [the local checklist](13_Local_Docker_Browser_Test_Checklist.md). Group pricing/reservation remains transitional; this is not production or full rental-flow acceptance.

## September 26, 2026: reservation day consistency

Reservation DTO day counts now preserve stored pricing snapshot days; records without a snapshot use the same Turkey rental calendar as pricing. Availability uses quoted days with the same fallback. This removes 3-day/4-day mismatches without rewriting historical quotes. Homepage search defaults were corrected with unchanged Header/Hero and form layout. Validation: 827 backend, 54 API integration, 308 frontend tests; build/TypeScript/lint passed (one existing warning). See the implementation evidence and handoff for local browser proof and pending new-head CI/review.

## September 25, 2026: third catalogue review

Public vehicle detail now exposes the existing API minimum-age and licence-tenure values with five-locale labels. Confirmation/tracking display stored UTC values in Europe/Istanbul, including calendar dates. Admin behavior is unchanged. 304 frontend tests, production build/TypeScript and lint passed; one pre-existing lint warning remains. Synthetic-API local browser checks verified both fixes; no backend rerun, real booking, merge or deployment. See the updated implementation evidence and handoff.
