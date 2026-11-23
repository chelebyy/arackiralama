# Guest reservation account isolation

Finding: `reservation.guest-email-account-linkage` (occurrence `occ_35f48bd6fcbf4ac165498bbe`).
Base revision: `1a352c984972a3c6a705b10073563eca470b92fc`.

## Accepted behavior

Public draft and unpaid checkout remain available without signing in, including when
contact email matches a registered or passwordless customer. Email is unverified
contact information, not authority to attach a booking to an account. Public
checkout creates no customer record and assigns no CustomerId, even if an account
with the submitted email exists. Contact name, email, phone, identity number, and
locale are stored on the reservation; existing driver snapshots retain driver data.

New guest bookings do not appear in account history. Registration and existing
account-claim flows do not automatically attach them by email. A future verified
booking-claim workflow must establish ownership explicitly; this patch does not
introduce that workflow. Existing historical and staff-created account associations
are preserved; historical email-only ownership is not retroactively certified.

## Boundaries and compatibility

Both public creation endpoints emit an allowlisted receipt: id, publicCode, status.
Booking id remains necessary for the existing hold/payment protocol. Projection
also applies to service-level quote replay, including old linked reservations.
No customer profile, account statistics, driver data, notes or plate leave these
creation endpoints. Admin/customer-authorized service DTOs retain their contract,
with nullable CustomerId and reservation contact values for guests. Guests have
zero account statistics, rather than aggregate statistics for all ownerless rows.

Admin search and contact editing support ownerless bookings. Confirmation,
cancellation, payment/deposit and reminder notification resolution uses the saved
guest destination and locale. Legacy and manual bookings retain customer lookup.
Customer account queries continue to filter on CustomerId, excluding guests.

The idempotency cache namespace is versioned globally to stop pre-fix customer
responses from being replayed through any idempotent endpoint. Creation cache keys
also include route identity; quote-based requests retain durable quote replay.
Existing Redis response-cache entries become misses after rollout. Review pending
legacy retries during rollout; provider/database idempotency remains unchanged.

## Migration and rollout

`20261021090000_IsolateGuestReservationContacts` makes customer_id nullable and adds
nullable guest contact columns. It does not rewrite historical ownership or deploy
anything. Legacy rows fall back to their linked customer, so no contact backfill is
needed. The down migration refuses to discard guest data while ownerless bookings
exist. Deploy schema and API together; do not roll back to code that assumes every
reservation has a customer while guest rows exist.

## Verification

Regression coverage includes existing registered/passwordless emails with case and
whitespace variations, both public creation routes, durable quote replay, public
response allowlisting, old response-cache bypass, guest admin search/editing,
account-history exclusion, zero guest account statistics, and notification delivery
through both reservation and payment services. Existing exact-vehicle, payment,
manual reservation and account-claim suites remain applicable.

Local .NET build/test is unavailable: SDK absent and its download host is blocked
by the execution environment. Frontend dependency installation was also blocked.
CI results must be recorded before declaring the fix verified. No production test,
migration, deployment, or merge is authorized by this change.
