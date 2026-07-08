# Admin Public Site & Contact UX Refresh Implementation

**Created:** 2026-07-08  
**Scope:** Admin usability refresh for Public Site & Contact authoring  
**Primary user:** Site owner/admin editing customer-facing content  
**Verification gate:** Focused admin browser smoke in Docker Desktop
**Implementation status:** Completed for the focused slice on 2026-07-08

## 1. Summary

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
