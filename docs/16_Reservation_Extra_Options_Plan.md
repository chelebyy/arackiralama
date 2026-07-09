# Reservation Extra Options Plan

**Created:** 2026-07-09
**Scope:** Admin-managed, vehicle-group-specific reservation extra options
**Primary users:** Customers making a booking; Admin and SuperAdmin operators
**Source of truth:** This document defines the approved product and architecture decisions
**Verification gate:** Focused backend/frontend tests plus Docker Desktop browser validation
**Plan status:** Approved for implementation

Related execution document: [17_Reservation_Extra_Options_Implementation.md](17_Reservation_Extra_Options_Implementation.md)

## 1. Summary

The booking flow currently defines child seat, additional driver, GPS, and Wi-Fi independently in public Step 3 and Step 4. The browser presents all four as priced options, but the backend only prices child seat and additional driver. GPS and Wi-Fi therefore appear billable without affecting the persisted reservation or payment total. Selected options are passed through the URL, and reservations do not retain immutable extra-option snapshots.

This change replaces those hard-coded definitions with a server-owned catalog. Admin and SuperAdmin users will be able to create, edit, activate, deactivate, order, assign, archive, and conditionally delete reservation extra options. Customers will see only active options assigned to the vehicle group they selected.

The server remains the sole price authority. The browser sends option identifiers and quantities to obtain a short-lived, session-bound server quote; it never sends trusted names or prices. Reservation creation consumes that quote and revalidates activity, vehicle-group assignment, and maximum quantity. A reservation stores both immutable option rows and a versioned full-pricing snapshot so later catalog or pricing-rule edits cannot rewrite booking history.

## 2. Confirmed Product Decisions

- Availability is configured by vehicle group, not by individually assigned vehicle.
- One option may be assigned to multiple vehicle groups.
- The same option uses the same price and maximum quantity in every assigned group. A different group price requires a separate option.
- Supported pricing modes are `PER_DAY` and `PER_RENTAL`; a zero price represents a free option.
- Customers may select quantities from zero through the admin-defined maximum.
- V1 does not track physical stock, date-based capacity, or individual-vehicle compatibility.
- Turkish, English, German, Russian, and Arabic names and descriptions are required before activation.
- An inactive draft may be saved with incomplete translations or without a vehicle-group assignment.
- Admin and SuperAdmin roles may manage the catalog through the existing `AdminOnly` authorization policy.
- Used options are archived instead of deleted. Unused options may be permanently deleted.
- Existing four options are migrated and assigned to all vehicle groups that exist when the migration runs.

## 3. Goals

- Make the visible list, price, pricing mode, order, quantity limit, and vehicle-group availability admin controlled.
- Eliminate duplicated Step 3 and Step 4 option definitions.
- Include every displayed option in the authoritative server quote and reservation total.
- Preserve the complete historical price breakdown, option names, prices, quantities, and totals after catalog or pricing-rule changes.
- Keep the public flow optional and recoverable when no extra option is available.
- Preserve legacy API clients during a defined compatibility window.
- Provide audit, concurrency, validation, and browser evidence suitable for a release gate.
- Provide an observable and measurable exit from the legacy compatibility path.

## 4. Non-Goals

- Selecting or reserving a specific plate/vehicle during the public booking flow.
- Physical inventory or availability calendars for child seats, devices, or accessories.
- Different prices or quantity limits per vehicle-group assignment.
- Percentage-based, seasonal, office-specific, or date-range-specific extra pricing.
- Multiple currencies; V1 uses `TRY` consistently with current reservation pricing.
- Arbitrary icon uploads, remote icon URLs, SVG input, or HTML content.
- Making any extra option mandatory.
- Replacing the existing campaign, pricing-rule, airport-fee, one-way-fee, young-driver, or coverage-waiver behavior.

## 5. Current-State Findings and Disposition

| Finding | Current behavior | Required disposition |
|---|---|---|
| Catalog ownership | Step 3 and Step 4 each contain a hard-coded option array | Replace both arrays with one public catalog endpoint |
| Child seat price | Browser shows 10 TRY/day; server charges 75 TRY/day | Seed and display 75 TRY/day, then allow admin edits |
| Additional driver price | Browser shows 15 TRY/rental; server charges 150 TRY/rental | Seed and display 150 TRY/rental, then allow admin edits |
| GPS price | Browser shows 8 TRY/day; server ignores it | Seed 8 TRY/day and include it in server totals |
| Wi-Fi price | Browser shows 12 TRY/day; server ignores it | Seed 12 TRY/day and include it in server totals |
| Selection transport | Comma-separated option codes are put in the URL | Persist typed selections in the booking store |
| Reservation history | Only the final reservation total is persisted | Persist one immutable row per selected option |
| Admin placement | No reservation-extra settings page exists | Add a `Reservation Extras` tab under Settings |

## 6. Domain and Data Model

### 6.1 `ReservationExtraOption`

The catalog aggregate contains:

- `Id: Guid`
- `Code: string` — unique, immutable, server generated for new options
- `UnitPrice: decimal(18,2)`
- `PricingMode: ReservationExtraPricingMode` — `PerDay` or `PerRental`
- `MaxQuantity: int`
- `IconKey: string`
- `SortOrder: int`
- `IsActive: bool`
- `IsArchived: bool`
- `Version: uint` — PostgreSQL `xmin` row version
- standard `CreatedAt` and `UpdatedAt` timestamps

`Version` is mapped to PostgreSQL `xmin` with `IsRowVersion()` and is the only optimistic-concurrency value accepted by admin writes. `UpdatedAt` remains informational. Any translation or vehicle-group assignment mutation must also update the parent option row so its `xmin` changes.

### 6.2 `ReservationExtraOptionTranslation`

- Composite primary key: `OptionId + Locale`
- `Locale` is one of `tr`, `en`, `de`, `ru`, or `ar`
- `Name` has a maximum length of 100 characters
- `Description` has a maximum length of 300 characters
- Deleting an unused option cascades to its translations

### 6.3 `ReservationExtraOptionVehicleGroup`

- Composite primary key: `OptionId + VehicleGroupId`
- Duplicate assignments are prohibited
- Deleting an unused option cascades to assignments
- Deleting a vehicle group cascades only its option-assignment rows; the option itself remains

### 6.4 `ReservationSelectedExtra`

This is the immutable reservation snapshot:

- `Id: Guid`
- `ReservationId: Guid`
- `ExtraOptionId: Guid`
- `OptionVersionSnapshot: uint`
- `Locale: string`
- `OptionCodeSnapshot: string`
- `NameSnapshot: string`
- `DescriptionSnapshot: string`
- `UnitPriceSnapshot: decimal(18,2)`
- `PricingModeSnapshot: ReservationExtraPricingMode`
- `Quantity: int`
- `RentalDaysSnapshot: int`
- `TotalPriceSnapshot: decimal(18,2)`
- `Currency: string`, fixed to `TRY` in V1
- standard creation timestamp

The reservation relationship cascades on reservation deletion. The option relationship uses `Restrict`; an option referenced by a snapshot cannot be hard deleted.

The table has a unique constraint on `ReservationId + ExtraOptionId`, preventing duplicate snapshot rows for the same reservation.

`Reservation` also gains nullable `QuoteId` with a unique index. New quote-based reservations persist it; legacy reservations leave it null. This provides a database replay barrier and lets a retry return the reservation already created from a quote.

### 6.5 `ReservationPricingSnapshotV1`

Every new-format reservation stores one versioned full-pricing snapshot in a `jsonb` reservation column. The snapshot is a persistence contract, not a serialized API DTO, and contains:

- `SchemaVersion: 1`
- daily rate, rental days, and base total
- airport, one-way, young-driver, coverage-waiver, and other fee lines
- campaign identifier/code, discount type/value, and discount total when applied
- generic extra line items and extras total
- deposit and pre-authorization amounts
- currency and final total
- quote identifier, issue time, and expiry time

New reservation details are built from this snapshot. Pre-migration reservations retain the existing total-only fallback and return `breakdownSource: LEGACY_TOTAL_ONLY`; new reservations return `breakdownSource: SNAPSHOT`. The API must not present reconstructed legacy values as an exact historical breakdown.

### 6.6 Database Constraints

- `UnitPrice` must be between 0 and 1,000,000 TRY.
- `MaxQuantity` must be between 1 and 20.
- `SortOrder` must be between 0 and 9,999.
- `IconKey` must belong to the server allowlist.
- `Currency` must equal `TRY` in V1.
- Non-null reservation `QuoteId` values must be unique.
- Translation locale, name, and description limits are enforced by both request validation and database shape.
- Active options must have all five translations and at least one vehicle-group assignment.

## 7. API Contracts

### 7.1 Public Catalog

`GET /api/v1/reservation-extra-options?vehicleGroupId={guid}&locale={locale}`

The response is ordered by `sortOrder`, then localized name, and contains only active, non-archived options assigned to the requested group.

```json
{
  "items": [
    {
      "id": "guid",
      "code": "child_seat",
      "name": "Çocuk Koltuğu",
      "description": "...",
      "unitPrice": 75.00,
      "pricingMode": "PER_DAY",
      "maxQuantity": 3,
      "iconKey": "baby",
      "sortOrder": 0,
      "version": 42
    }
  ]
}
```

Unsupported locales, missing vehicle groups, and malformed identifiers return `400`. An empty valid catalog returns `200` with an empty `items` array.

The response uses `Cache-Control: no-store`. The SWR key must include vehicle group and locale, and catalog reads are revalidated when either changes.

### 7.2 Public Quote

`POST /api/v1/pricing/quote`

The request contains the existing vehicle, office, date, campaign, driver-age, and coverage inputs plus:

```json
{
  "locale": "tr",
  "selectedExtras": [
    {
      "optionId": "guid",
      "quantity": 2,
      "optionVersion": 42
    }
  ]
}
```

The response extends the current price breakdown with `extraItems`, an opaque `quoteId`, and `expiresAtUtc`. Each extra item returns the authoritative localized snapshot candidate, unit price, pricing mode, quantity, rental days, and total.

The server stores `ReservationQuoteV1` in Redis for 15 minutes, matching the existing reservation-hold window. The quote is bound to `X-Session-Id` and contains normalized price-driving inputs, the complete pricing snapshot, selected option identifiers/versions/quantities, issue time, and expiry time. The client cannot derive, alter, or choose the quoted price.

Price-only catalog changes after quote creation do not change the customer's price during the 15-minute validity window. Deactivation, archive, vehicle-group removal, or a reduced maximum quantity invalidates the quote at submission. Quote responses use `Cache-Control: no-store`.

### 7.3 Reservation Creation

The new reservation path gains:

- `Locale`
- `QuoteId`
- the existing `X-Session-Id` header requirement

Selected extras are sent only to the quote endpoint. The client sends no option name, description, pricing mode, price, or total during reservation creation. The server loads the quote, verifies session ownership, expiry, normalized booking-input equality, and current option availability, then atomically claims it in Redis. It writes the reservation, unique `QuoteId`, selected-extra rows, and full-pricing snapshot in one transaction.

Expired, missing, already-claimed, replayed, session-mismatched, inactive, archived, unassigned, or over-limit quotes return `409 Conflict` without creating a reservation. Malformed quote requests, duplicate selections, and invalid quantities return `400`. Claim uses an atomic Redis operation tied to the request/idempotency key. A failed transaction releases its owned claim; a successful transaction marks the quote consumed with the reservation ID. A retry first resolves the unique persisted `QuoteId` and returns the existing reservation.

### 7.4 Admin Catalog

- `GET /api/admin/v1/reservation-extra-options`
- `POST /api/admin/v1/reservation-extra-options`
- `PUT /api/admin/v1/reservation-extra-options/{id}`
- `PATCH /api/admin/v1/reservation-extra-options/{id}/status`
- `POST /api/admin/v1/reservation-extra-options/{id}/restore`
- `DELETE /api/admin/v1/reservation-extra-options/{id}`

The list supports `search`, `status`, `vehicleGroupId`, `includeArchived`, `page`, and `pageSize`. Write requests contain `version`, price, pricing mode, maximum quantity, icon key, sort order, vehicle-group identifiers, and translations.

The status endpoint performs activation/deactivation only. The delete endpoint returns `disposition: "deleted"` for unused records and `disposition: "archived"` for used records. Restore returns an archived record to inactive draft; normal activation validation must run before it can become public again.

### 7.5 Reservation Responses

Admin and customer-facing reservation detail DTOs gain `selectedExtras`, `breakdownSource`, and the full snapshot-backed price breakdown. Existing `PriceBreakdownDto` gains `extraItems`; for new reservations every monetary field comes from `ReservationPricingSnapshotV1`, and `ExtrasTotal` equals the snapshot line-item sum.

## 8. Pricing and Lifecycle Invariants

- Per-day total: `UnitPrice × RentalDays × Quantity`.
- Per-rental total: `UnitPrice × Quantity`.
- Monetary calculations use decimal arithmetic and are rounded to two decimal places only at the established pricing boundaries.
- Rental-day calculation follows the current pricing engine; the browser must not implement an independent authoritative rule.
- The server loads every selected option in one query and rejects missing, duplicate, inactive, archived, unassigned, stale, or over-limit selections.
- Catalog edits never modify existing `ReservationSelectedExtra` rows.
- Price-only edits do not alter an unexpired quote. Deactivation, archive, group removal, or a reduced maximum quantity invalidates an affected quote with `409`.
- Archiving forces `IsActive = false`, removes the option from public results, and retains translations and assignments for audit/history.
- An archived option may be restored to inactive draft through an audited action; it cannot become public without passing full activation validation again.
- Hard delete of an unused option is atomic. A concurrent reservation reference causes a deterministic conflict or archive fallback rather than an unhandled foreign-key error.

## 9. Admin Experience

Add `/dashboard/settings/reservation-extras` and a `Reservation Extras` settings tab.

The page must provide:

- loading, error, empty, filtered-empty, and populated states using existing admin primitives;
- search, status, vehicle-group, and archived filters;
- visible active/inactive/archived badges;
- create and edit dialogs or a focused editor;
- five locale tabs with completion indicators;
- vehicle-group multi-select;
- pricing mode, price, maximum quantity, sort order, and icon controls;
- explicit activate/deactivate action;
- delete/archive confirmation that explains the resulting disposition;
- restore action for archived records;
- dirty/saving/saved/conflict feedback;
- accessible labels for icon-only actions.

Allowed icon keys are `baby`, `users`, `navigation`, `wifi`, `shield`, `briefcase`, `snowflake`, and `plus`. The frontend maps these keys to bundled Lucide icons; the backend rejects any other value.

## 10. Public Booking Experience

- Step 3 loads options after the vehicle group is known.
- Quantity controls support zero through `maxQuantity` and expose accessible increment/decrement labels.
- Cards show localized name, description, pricing mode, unit price, and calculated display total.
- Selections are stored in the persisted booking store as `optionId`, `quantity`, and `optionVersion` rather than in the URL.
- Step 4 obtains a server quote on entry and whenever campaign or selected extras change.
- Payment and confirmation use only the server quote and persisted reservation values; the UI shows the quote expiry time.
- Catalog-load failure shows a retryable warning and permits continuing with no extras.
- A `409` preserves customer/driver fields, reloads the catalog and quote once, identifies changed selections, and requires customer confirmation before resubmission. Automatic refresh is limited to one attempt to prevent retry oscillation.

For one compatibility release, Step 4 recognizes legacy `extras=child_seat,...` query values and maps only the four seeded codes to current catalog records. Unknown codes are ignored with a visible warning. Newly generated links never include the `extras` parameter.

## 11. Migration and Compatibility

The migration creates the catalog/snapshot schema, a `jsonb` pricing-snapshot column, indexes, constraints, and deterministic built-in identifiers. It copies the existing five-locale messages into translation rows and uses an `INSERT ... SELECT` to assign the four built-ins to every vehicle group present at migration time.

If no vehicle group exists during migration, the built-ins remain inactive. After optional runtime inventory seeding, an idempotent one-time backfill assigns the built-ins only when they still have no assignments, then activates them when all activation requirements are satisfied. Vehicle groups created later through normal admin workflows are not assigned automatically.

Legacy request fields `ExtraDriverCount` and `ChildSeatCount` remain accepted during a measured compatibility period:

- legacy-only requests map the counts to the seeded `additional_driver` and `child_seat` records;
- new clients create a server quote and submit its `QuoteId`;
- requests containing a `QuoteId` and non-zero legacy counts are rejected;
- the existing GET pricing breakdown endpoint remains available for existing callers;
- the new public application uses the POST quote endpoint exclusively.

Built-in codes remain immutable and cannot be hard deleted while compatibility mode is active. Legacy mapping never bypasses active/group/quantity validation. Structured logs record legacy adapter usage, unknown legacy codes, and resulting reservations. The adapter may be removed only in a separate cleanup release after 14 consecutive production days with zero legacy requests.

No existing reservation is backfilled with guessed extra selections. Historical rows created before this migration continue to return an empty `selectedExtras` list.

## 12. Security Requirements

- Use `AuthPolicyNames.AdminOnly` for all admin catalog endpoints.
- Treat frontend route protection as usability only; authorization is enforced at the API.
- Never accept client-supplied price, localized content, total, or pricing mode during quote or reservation creation.
- Validate locale, lengths, numeric bounds, identifiers, group membership, and option state server side.
- Use EF parameterization and avoid string-built SQL except bounded, constant migration SQL.
- Write audit events for create, update, activate, deactivate, group-assignment change, delete, and archive actions without customer PII.
- Apply the existing standard rate limit to catalog/quote reads and the existing strict/idempotent reservation protections to creation.
- Use PostgreSQL `xmin` concurrency for admin writes and the session-bound Redis quote lifecycle for customer submission.
- Bind Redis quotes to the public session, expire them after 15 minutes, and prevent cross-session replay.
- Return `Cache-Control: no-store` for public catalog and quote responses.
- Do not log full reservation requests, customer data, or opaque authentication values when reporting validation failures.
- Introduce no new third-party dependency for this feature.

Per repository policy, implementation is not security-complete until `aikido_full_scan` has received the full content of every added or modified first-party code file and reports zero unresolved issues. If the Aikido MCP server is unavailable, record the blocker and follow the official setup guide: https://help.aikido.dev/ide-plugins/aikido-mcp.

## 13. Observability, Deployment, and Rollback

Required structured events are:

- quote created, expired, invalidated, consumed, replayed, or session rejected;
- public catalog failure and empty-result rate;
- legacy adapter use and unknown legacy code;
- reservation created with extras and selected-extra count;
- admin archive, restore, hard-delete conflict, and concurrency conflict.

Logs must contain correlation/session-safe identifiers and option IDs/codes, never customer PII, card data, cookies, or raw quote payloads. Because the repository does not currently expose an application-metrics pipeline, structured logs and audit events are mandatory; adding unexported counters alone does not satisfy observability.

Deployment order is fixed:

1. Apply the additive schema and deploy the backward-compatible backend.
2. Verify migration, seed/backfill, legacy requests, quote storage, and reservation reads.
3. Deploy the admin frontend.
4. Deploy the public frontend.
5. Observe quote failures and legacy usage before declaring rollout complete.

Frontend rollback is supported. After the first new-format reservation is persisted, backend/schema down migration is prohibited because it would destroy pricing evidence. Backend failures use forward-fix or restore-from-verified-backup procedures; the down migration is only a pre-data development aid.

## 14. Acceptance Criteria

- Admin and SuperAdmin can create, edit, activate, deactivate, archive, and conditionally delete options.
- Archived options can be restored only to inactive draft and must pass activation validation again.
- An option cannot activate without five complete translations and at least one vehicle group.
- The public catalog differs correctly between two vehicle groups.
- Per-day, per-rental, free, multi-quantity, and maximum-quantity cases calculate correctly on the server.
- GPS and Wi-Fi affect the authoritative reservation and payment total.
- Client price manipulation does not alter the quote or reservation.
- Quote expiry, replay, session mismatch, and availability invalidation create no reservation and produce a bounded recoverable customer flow.
- A price-only admin edit does not change an already issued, unexpired quote.
- Changing or archiving an option leaves an existing reservation snapshot unchanged.
- New reservation details reproduce every monetary field from the full pricing snapshot without double counting; legacy rows are explicitly marked total-only.
- Admin reservation detail displays snapshot name, quantity, unit rule, and total.
- Legacy child-seat and additional-driver requests remain functional during the compatibility period.
- Legacy usage is measurable, and adapter removal is blocked until 14 consecutive production days with zero usage.
- Backend build/tests and frontend lint/tests/build pass.
- Docker Desktop browser validation proves admin authoring, group filtering, five locales, quote totals, reservation creation, history preservation, and clean browser console/network behavior.
- Evidence is recorded in [13_Local_Docker_Browser_Test_Checklist.md](13_Local_Docker_Browser_Test_Checklist.md) and status is synchronized in [10_Execution_Tracking.md](10_Execution_Tracking.md).

## 15. Completion Boundary

The feature is complete only when the schema, server pricing, full-pricing and selected-extra snapshots, Redis quote lifecycle, admin authoring, public booking flow, compatibility telemetry, backend-first deployment smoke, automated tests, Docker/browser evidence, documentation updates, and mandatory Aikido result are all closed. Unit tests or a successful build alone do not satisfy this boundary.
