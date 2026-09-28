# Package 3: short checkout and guest reservation management

Publication handoff and next-session checklist: [Package 3 handoff](Vehicle_Reservation_Package_3_Handoff.md).

Status: locally implemented. See [implementation and validation](Vehicle_Reservation_Package_3_Implementation.md). Remote CI, deployment and real-data activation remain separate.

Accepted operator decision: admins configure cancellation and date-change rules; missing settings keep the operations disabled. The following sections retain the original implementation plan and its release boundaries.

## Verified baseline

- On 2026-09-27, GitHub reported PR #447 as MERGED at 12:20:45 UTC, merge commit `1a352c984972a3c6a705b10073563eca470b92fc`. All returned completed checks/statuses succeeded except the intentionally skipped Docker Push to GHCR job. This does not prove deployment.
- Remote HEAD resolves to `main` at that commit. Package 1 was merged as `f1c34fe` in PR #446.
- Package 2 implementation and scoped local browser acceptance are documented on main in `Vehicle_Reservation_Package_2_Completion_Review.md` and `Vehicle_Reservation_Package_2_Acceptance.md`. Browser evidence precedes later review corrections; this planning session did not rerun tests or browser acceptance.
- The primary checkout remains on `codex/docs-db-ops-phase0-doc-contract` at `e81eb08`, with unrelated untracked content. Its untracked Turkish roadmap is the original September 24 proposal. Main has a newer roadmap, but its PR-pending/no-merge wording also predates the verified merge. Preserve both sources until a deliberate documentation reconciliation.
- Package 2 is complete as merged implementation with recorded local acceptance. Real operator settings, production migration/restore validation and authorized rollout remain separate.

## Original starting action (completed)

Start **3A: data minimization and a short checkout contract**. Do not begin with a new mail provider or homepage redesign. Complete the declaration contract before replacing the current exact-booking age/licence checks; removing fields alone would break or weaken those checks.

Before implementation, freshly fetch the actual remote default, safely synchronize local default under the repository policy, and use a suitable free checkout or an isolated worktree from that verified base. Do not switch, reset or overwrite the dirty primary checkout. Reconcile roadmap completion wording on that branch. This plan file is a local planning artifact, not a feature branch or PR.

## Existing boundaries to preserve

- Exact selected vehicle, server-calculated prices, explicit changed-offer acceptance, replay identity and PostgreSQL overlap protection.
- Confirmed reservation and payment status remain distinct; current payment-at-pickup flow remains.
- Current offices/delivery scope, five locales, Arabic RTL and public visual language. Protected header/hero remains unchanged.
- No account is required for the proposed flow; existing customer-account endpoints retain their authorization.
- Customer/driver data stays out of persistent browser storage. Historical accepted financial/condition snapshots are not silently rewritten.

## Evidence informing the work

Inspected at `origin/main` matching the verified remote HEAD:

- `backend/src/RentACar.API/Contracts/Reservations/ReservationRequests.cs`: customer and driver contracts still accept identity/licence numbers and full dates of birth; create requests also carry DriverAge.
- `backend/src/RentACar.Core/Entities/Customer.cs` and `Reservation.cs`: legacy sensitive-data fields remain. Their existence does not establish that real personal data exists.
- `backend/src/RentACar.API/Services/ReservationService.cs`: sensitive driver fields are still mapped; changing a quoted itinerary/driver explicitly requires a new quote and reservation. A guest amendment needs a dedicated operation, not removal of this guard.
- `backend/src/RentACar.API/Controllers/CustomerReservationsController.cs`: existing management requires CustomerOnly authorization. It is not a ready-made guest endpoint.
- Existing notification interfaces, queue, templates and SMTP provider can be reused. Provider-level delivery guarantees were not audited.

## Implementation sequence and acceptance

### 3A — Minimal data and short checkout

Scope:

1. Map sensitive fields through public/admin requests, validation, entities, accepted snapshots, replay fingerprints, responses, notifications and logs. Inventory stored data by field presence/count only; do not print values or inspect production without an authorized environment scope.
2. Define a versioned declaration contract for age at pickup and licence tenure/eligibility without full birth date or licence number. Proposed model: declared age at pickup and confirmed required licence tenure; this is a declaration, not identity verification. Operator confirmation is needed for the accepted declaration and document checks at handover. If pickup date changes, reconfirm eligibility rather than infer a birthday from stored age.
3. Remove prohibited fields from all active collection paths, including admin/manual reservation paths. Define explicit safe handling of legacy request fields: they must never be persisted, echoed, logged or mailed. Do not silently retain them through permissive binding or old clients.
4. Implement contact/delivery details -> price and conditions review -> confirmation. Preserve date/vehicle search before these steps, old entry links, back/reload recovery and explicit offer acceptance. Delivery-specific fields remain conditional on existing supported delivery modes.
5. Preserve quote validity and idempotency under the new schema; define compatibility for old accepted snapshots and pending attempts. Do not reinterpret old replay proofs as new-schema requests.
6. Document storage classification, retention decisions, key separation, encrypted-field search requirements, rotation/recovery and backup lifecycle. Prepare a staged migration; destructive historical cleanup is a separate explicitly scoped action after inventory and preservation decisions.

Acceptance:

- No identity number, licence number or full date of birth is collected or newly persisted through any active reservation creation/update path.
- A crafted legacy payload cannot bypass this property. Tests cover persistence, DTOs, notification payloads and captured logs without including real PII.
- Eligibility declarations enforce configured vehicle rules; Turkey pickup-calendar boundaries, changed dates and stale/replayed quotes remain covered.
- Exact vehicle selection, one reservation per retry, unpaid confirmation and protected UI remain intact. No new persistent browser PII.
- Existing historical reservations remain readable and financially unchanged. No production purge or blanket column drop is part of this slice.

### 3B — Guest access and reliable email foundation

Recommended design, to be confirmed before implementation: reservation reference plus email initiates a one-time verification link/code; successful verification establishes a short-lived, reservation-scoped management session. A reference or email alone grants no mutation authority.

- Separate guest authorization from CustomerOnly/admin permissions. Bind access to one reservation and explicit allowed operations, with expiry, revocation and re-verification for sensitive contact changes.
- Store verification secrets as hashes. Define TTL, retry limits and per-target/IP abuse limits; use neutral responses to avoid revealing bookings or email addresses.
- Email-link GET must not cancel/change a reservation or consume authorization solely because a mail scanner opened the link. Require an explicit exchange/confirmation; prevent replay and referrer/log leakage.
- Use secure server-controlled sessions, request forgery protection for mutations and no secret persistence in browser localStorage. Validate all resource access server-side.
- Reuse the existing queue/template/provider abstraction. Exercise a local mail sink first; keep provider setup and real sending separately authorized. Resend remains a candidate, not an approved dependency or verified service configuration.
- Model booking commit, enqueue, retries, deduplication and permanent delivery failure explicitly. Do not promise exactly-once SMTP delivery after an ambiguous provider timeout. Mail failure must not turn a committed booking into a failed booking result.

Acceptance: booking enumeration, wrong/expired/replayed secrets, cross-reservation access, brute force, mail-scanner GET, CSRF and session revocation tests; five-language messages; retry/recovery tests with one business event and observable failed delivery. No real customer email during tests.

### 3C — Guest view and cancellation

- Present an allowlisted private booking view after verified access. Keep public tracking minimal and distinct from management authorization.
- Implement cancellation as a server-validated, idempotent state transition with scoped authorization and an audit record that omits secrets/PII.
- Apply an explicitly approved cancellation policy and show its outcome before confirmation. Do not invent fees, free-cancellation promises, refunds or no-show rules. Do not introduce an online payment/refund provider.
- Commit state transition and notification event consistently; repeated requests return the same effective outcome.

Acceptance: another reservation's ID is rejected; disallowed states/times are rejected; cancellation retries do not duplicate side effects; stock release and preparation occupancy remain consistent; email outage does not undo success.

### 3D — Atomic date changes

- Add a dedicated amendment quote and confirmation operation. Bind quote to reservation, current version, exact vehicle, new itinerary, extras, current policies and eligibility declaration.
- Recalculate availability, full price and preparation interval; require explicit acceptance of new total/conditions. Show any approved change charge separately; unspecified charge rules must not be invented.
- Atomically replace the active booking interval and current accepted snapshot while preserving immutable amendment history. Failure leaves the original reservation, amount and occupancy intact.
- Preserve exact vehicle identity. Use database-enforced conflict protection and replay-safe concurrency control; do not cancel the old reservation first or merely remove the existing itinerary-update guard.
- Concurrent cancellation, amendment and admin action must have one consistent outcome. Confirmation mail is an event after successful commit.

Acceptance: real PostgreSQL/Redis tests for competing amendments, conflict with another booking, cancellation race, expired quote, changed policy/price, response loss and duplicate submission; rollback retains the original booking and failed changes send no success email.

### 3E — Integrated acceptance and documentation

- Run appropriate backend units and real PostgreSQL/Redis integration cases, frontend tests, type checks, lint and production build for changed paths. Run project-required CI before merge.
- Browser matrix: desktop/tablet/mobile, all five locales, Arabic RTL, keyboard, back/reload, stale offer, lost response, expired access, cancellation/amendment and mail failure. Record physical-device/accessibility limitations separately.
- Update roadmap, implementation evidence, `13_Local_Docker_Browser_Test_Checklist.md` and `15_Admin_UX_Refresh_Implementation.md` before commit/publication.
- Offer a focused security review after implementation and a stack-aware check plan before release/handoff. Keep local validation, remote CI, merge and production activation separate.

Suggested PR slices: 3A; 3B; 3C; 3D; final acceptance follow-up only if it needs changes. Each slice must be usable/testable within its boundary. Package 3 remains incomplete until all accepted scope is verified.

## Decisions and gates

| Decision | Proposed direction | Must be settled before |
|---|---|---|
| Age/licence declaration and handover check | Minimal declaration, no full birth date or licence number | 3A contract and validation |
| Delivery address/flight details | Only fields required by supported delivery mode | 3A form acceptance |
| Retention and encrypted-field design | Classified fields, separate keys, recoverable rotation; no arbitrary retention period | Real data collection and destructive cleanup |
| Guest access method/TTL/retry policy | One-time email verification into reservation-scoped short session | 3B implementation |
| Email provider, domain and data handling | Local sink first; evaluate existing SMTP versus candidate Resend | External setup/real sending |
| Cancellation, no-show, amendment and price-difference policy | Explicit operator rules; no fabricated charges/refunds | 3C/3D behavior and copy |

These decisions do not block the initial field inventory and contract proposal. Unresolved financial policies must not block independent data-minimization work or be replaced with assumed production behavior.

## Planning security gaps

Advisory requirements, not newly proven vulnerabilities. Stage: plan. Stack: ASP.NET Core, PostgreSQL/Redis, Next.js.

| ID / severity / confidence | Evidence and failure scenario | Required control and verification |
|---|---|---|
| P3-DATA / high / high | Current request/entity paths accept prohibited data; hiding inputs leaves crafted requests and legacy paths | End-to-end minimization, binding/persistence/log/email tests; historical inventory and separately authorized cleanup |
| P3-AUTH / high / high | Roadmap proposes guest mutations while existing account endpoint is CustomerOnly; a reference-only replacement could expose another booking | Reservation-scoped expiring authorization; replay, enumeration, CSRF, brute-force and cross-resource tests |
| P3-ATOMIC / high / high | Exact itinerary edits currently reject; weakening that guard could lose accepted price/occupancy | Dedicated versioned amendment transaction; real database races and rollback assertions |
| P3-MAIL / medium / medium | Proposed management depends on mail; retries and scanners can trigger duplicate effects or consume access | Safe GET behavior, explicit exchange, event deduplication, bounded retries and ambiguous-delivery tests |
| P3-KEYS / high / medium | Retention/encryption are roadmap requirements without selected operational policy | Field/key/backup design and recovery proof before real data; do not treat encryption as authorization |

Reviewed: roadmap versions, merged Package 2 reports, selected current reservation contracts/service boundaries, management authorization declaration and notification structure.

Not reviewed: full application security, production data/backups, provider accounts, actual mail delivery, legal compliance, deployment or runtime behavior in this session.

Assumptions: no live activation in this scope; current exact-vehicle and payment-at-pickup decisions persist; policy values remain operator decisions.

Tools run: read-only Git/remote checks, GitHub PR/check lookup, scoped source/document reads, Codex Sentinel planning checklist. No tests, scanner or browser run. No library syntax or provider setup is prescribed here; consult Context7/official documentation when those implementation choices are made.
