# Package 2: exact vehicle, pricing and automatic reservation

Updated September 26, 2026. Implementation and scoped local acceptance are complete. Real browser checks cover five locales, mobile/RTL, group-less authoring, changed-offer acceptance, concurrent exact allocation, response-loss replay and office policy boundaries; see the [acceptance record](Vehicle_Reservation_Package_2_Acceptance.md). Changes are uncommitted on `codex/catalogue-next-plan`; no Package 2 PR, deployment or production migration has occurred.

## Accepted business decisions

- Preserve Turkey calendar-day billing, minimum one day. October 10 01:00 to October 13 09:00 remains three days.
- Reserve the exact selected vehicle; never substitute another vehicle from its legacy group.
- An eligible no-online-payment booking becomes `Confirmed`, with payment due at pickup. It creates no payment intent or paid event.
- Operators must explicitly configure minimum notice, pickup and return windows, closed dates and preparation duration. Missing policies prevent exact-vehicle booking. Synthetic test values are not production defaults.
- Copy existing group prices/conditions to vehicles initially; operators then edit vehicle terms directly. New vehicles can be authored without a group.
- Changed prices or conditions require a fresh quote and customer acceptance. Historical reservations and accepted snapshots remain unchanged.
- Keep the existing office/delivery scope, pricing formula, campaign behavior and rounding. No new address-delivery product, provider, late-return fee or guest-management workflow is introduced.

These decisions supersede the first-slice limitations in historical records. See the [current plan](Vehicle_Reservation_Package_2_Plan.md).

## Implemented behavior

### Vehicle policies and migration

`Vehicle.RentalTerms` stores deposit, minimum age/licence years, seasonal rates and eligible extra IDs. Rules preserve date range, original ID, priority, creation timestamp, calculation type and weekday/weekend multipliers. The existing pickup-date rule selection and Turkey day calculation are retained.

`Office.OperatingPolicy` stores mandatory notice/preparation minutes, separate pickup/return windows and closed dates. Windows use Turkey local time and half-open boundaries; preparation is taken from the return office. Policy inputs are validated, including invalid or overlapping windows, missing values and bounded collection sizes.

Admin vehicle and office dialogs edit these values. Group membership is optional; group-less vehicle creation requires its own valid terms. Existing extra selections outside the currently loaded page remain visible and removable. Public vehicle DTOs remain allowlisted and omit plates and internal policy payloads.

Migration `20260926125053_VehicleRentalPolicies` adds JSONB terms/policies, nullable vehicle group membership and nullable reservation occupancy end. It copies existing group rates/conditions/extra eligibility to each mapped vehicle. It does not invent operating hours or rewrite historical reservation totals, snapshots or statuses.

### Authoritative availability, quote and finalization

`VehicleBookingService` evaluates the actual vehicle, office membership, active offices, maintenance/status, operating policy, driver eligibility, selected extras and vehicle price. Exact intervals are bounded to 366 days. Missing rates or policies are unbookable. `GET /api/v1/vehicles/available-exact` returns actual public vehicles with server-calculated totals.

Quotes bind vehicle, itinerary, inputs, selected-extra versions and a fingerprint of effective terms, policies and pricing. Finalization recomputes the offer. An expired, changed or unavailable offer returns a conflict; a fresh quote must be accepted before submitting again. Birth/licence dates are checked at finalization against the accepted driver conditions.

The final transaction locks only the referenced office rows, selected vehicle, selected extras and selected campaign while revalidating. PostgreSQL exclusion covers the half-open interval through return plus the accepted preparation duration. Reservation and vehicle guards coordinate allocation with maintenance/retirement/office changes. Redis is supplementary coordination, not the stock authority.

Accepted preparation duration is persisted with the booking snapshot; later office edits do not resize earlier reservations. Exact draft-to-hold transitions revalidate policy and preserve the exact vehicle and session. Changed-policy holds return HTTP 409. Contact-only edits preserve the accepted contract; exact-booking itinerary/driver changes require a new quote/reservation.

### Confirmation, replay and compatibility

The existing unpaid endpoint now returns `Confirmed` for valid exact-vehicle payment-at-pickup quotes, with no unpaid expiry. Legacy group-only callers retain the existing unpaid-request workflow and expiry. Historical requests are not bulk-confirmed.

Replay schema 2 binds exact vehicle and accepted policy fingerprint; schema 1 remains compatible for historical/group-only requests. Quote-bearing requests bypass the generic response cache so service-level ownership and payload checks run even when an idempotency key is reused. A same-owner retry returns the original reservation; another session or changed vehicle is rejected.

The expiry service and worker continue to select legacy `UnpaidRequest` rows. Confirmed reservations have no unpaid expiry and cannot be expired through the legacy hold/request expiration method.

### Customer flow

Dated catalogue, vehicle detail and step 2 use the same authoritative vehicle availability/total. Undated catalogue browsing remains available. Steps retain actual vehicle identity, name and image; missing/unavailable vehicles block advancement with retry or explicit reselection.

Extras are filtered by vehicle eligibility. Final quote shows deposit, minimum age and licence years. Stale quote recovery preserves form inputs and requires acceptance of the replacement offer. Confirmation wording distinguishes confirmed/payment-at-pickup from the legacy unpaid request in all five locales. Failed summary lookup retains the code and offers retry without claiming an unverified status. Language changes preserve dynamic route and query parameters; known errors use translated recovery messages. Licence issue/expiry dates are required actual user inputs, and Back restores in-memory form data. Header/Hero have no source changes.

## Local verification

- Complete backend unit suite: **853 passed**.
- Complete PostgreSQL/Redis API integration suite: **66 passed**; final quote endpoint regression suite: **11 passed**, including one additional dated-catalogue test and expanded confirmed-expiry coverage. These runs cover 67 distinct API tests.
- Complete frontend suite under `TZ=America/Los_Angeles`: **70 files / 332 tests passed**. Final confirmation-error regression: **4 passed**, including one added case (333 distinct frontend tests across these runs).
- Frontend production build (`next build --webpack`) and TypeScript: passed.
- Frontend lint: zero errors; one existing unused-disable warning in SearchForm.test.tsx.
- Existing build warnings remain: workspace lockfile inference and middleware-to-proxy deprecation. They are unrelated to this package.
- Migration rehearsal downgraded/upgraded an isolated test database and verified copied rates/conditions, unconfigured offices, and unchanged historical snapshot/amount.
- Real database tests cover concurrent exact allocation without substitution, policy/price changes requiring a new quote, preparation boundaries, group-less authoring/booking, replay/session tampering, admin validation/authorization and maintenance protection.
- Browser acceptance: scoped local matrix passed against the production-build frontend and actual API/PostgreSQL/Redis. Final failed-summary retry and dated conflict-reselection checks also passed after the last build. See the [acceptance evidence](Vehicle_Reservation_Package_2_Acceptance.md).

Tests used loopback PostgreSQL port 5548 and Redis port 6388 with synthetic databases. No production credentials or customer data were used. Existing database/container data is retained.

## Security review and release checks

The [focused completion review](Vehicle_Reservation_Package_2_Completion_Review.md) records planning gaps, implementation controls, corrected findings and unreviewed boundaries. Reviewed areas are the changed quote/replay, availability/occupancy, admin policy and public DTO paths. This is not a repository-wide security certification.

The local entries of the stack-aware checklist in [document 13](13_Local_Docker_Browser_Test_Checklist.md) are complete with their evidence boundaries. Before release, complete actual operator configuration, representative production migration/restore review and normal PR/CI review. No additional broad security scan has been run.

## Rollout and preservation

1. Review and commit this worktree, then use the normal PR/CI/review process. Merge/deployment still needs operational authorization.
2. Before production migration, inventory vehicle/group mappings, missing rates and unknown terms without exporting customer data; rehearse against a sanitized representative database and measure migration locks/duration. Local synthetic tests do not prove production data readiness.
3. Retain a verified backup/restore path. Deploy schema and application compatibly; retain legacy group APIs for existing callers.
4. Enter actual operator-approved office policies and inspect migrated vehicle prices/conditions before enabling customer use. No default hours or preparation duration should be guessed.
5. If new exact bookings must stop, deactivate the affected office through its existing admin control while retaining bookings, snapshots and replay data. This also stops that office's other new bookings. Do not delete confirmed records or run the destructive Down migration as an operational rollback.
6. Schema rollback is unsafe once group-less vehicles or new contracts exist. The generated Down migration has a null-group guard but still removes policy columns; it was used only in an isolated migration test.

Active worktree: `C:/Users/muham/.codex/worktrees/catalogue-next-plan/Araç Kiralama`, branch `codex/catalogue-next-plan`, base/HEAD `f1c34fecde4b0b9a79ffa19201d744d1be33c6ba`. Fetched remote default and local main matched that commit at implementation start. The primary dirty checkout remains on its older branch and was not switched or reset.

The frontend node_modules junction points to the retained local-validation-20260923 worktree. Preserve both until dependency consumers are audited. No worktree cleanup, branch deletion, commit, push, Package 2 PR or release was performed.
