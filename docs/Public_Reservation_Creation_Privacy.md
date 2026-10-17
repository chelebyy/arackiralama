# Public reservation creation response privacy

Review date: 17 October 2026. Base: `main` commit `1a352c984972a3c6a705b10073563eca470b92fc`.

## Verified problem

Anonymous reservation creation selects an existing customer by the email supplied in the request. Both public creation actions returned the internal `ReservationDto`, which includes that customer's stored name, phone, customer identifier, reservation count and total spend, together with vehicle plate, driver information and internal reservation metadata. Supplying an existing customer's email therefore exposed stored data without verifying ownership of the email address.

Four real API regression cases reproduced the exposure across legacy group and exact-vehicle booking, for both draft creation and unpaid requests. Every request reached HTTP 200; all four failed the expected response allowlist assertion. Existing public GET response minimization in #415 did not cover these POST responses, and the guest-management work in #457 has a separate scope.

## Correction

Both `POST /api/v1/reservations` and `POST /api/v1/reservations/unpaid-requests` now explicitly map the service result to `PublicReservationCreatedDto`:

```json
{
  "id": "00000000-0000-0000-0000-000000000001",
  "publicCode": "EXAMPLE-CODE",
  "status": "Draft"
}
```

The usual API success envelope still surrounds this data. The frontend API functions and creation hook use the matching three-field `PublicReservationCreated` type. Checkout uses the reservation identifier for its next operation and the public code for confirmation/tracking. Internal service and administrator response contracts remain available to their existing callers.

The public creation endpoints declare `ResponseVersion = "public-create-v1"`. The generic idempotency middleware prefixes its local cache key and stores/checks that version in the cache envelope. The envelope check is necessary because an old caller-controlled key can already contain the new prefix; a prefix alone could collide with an old full-response entry. A dedicated regression reproduced that collision before the envelope check was added.

Missing or mismatched cache-envelope versions are ignored. The new response replaces an entry only through the existing successful-response caching path. Default empty versions retain existing behavior for other attributed endpoints. Incoming request headers are not modified, and quote/exact-vehicle requests continue through their existing service replay path.

## Validation

| Check | Result |
| --- | --- |
| Original controller privacy regressions | 2 failed before correction |
| Original cache-version regressions | 2 failed before correction |
| Additional old-envelope collision regression | 1 failed before envelope validation |
| Original real API privacy regressions | 4 failed on exposure assertions before correction |
| Full final backend unit suite | 888 passed, 0 failed, 0 skipped |
| Final Release solution build | Passed, 0 errors; 18 pre-existing warnings |
| Full final API integration suite | 150 passed, 0 failed, against isolated local PostgreSQL 16.15 and Redis 7 |
| Frontend TypeScript check | Passed |
| Frontend API, creation-hook and checkout tests | 46 passed across 3 files |
| Full frontend lint | Passed, 0 errors; 1 pre-existing unused-disable warning in `SearchForm.test.tsx` |
| Frontend production build | Passed, capped at 2 workers |
| Privacy implementation/new regression whitespace checks and complete `git diff --check` | Passed |

The final integration cases seed a stored customer and completed rental, create another reservation using that email, require exactly the three allowed response fields, and verify persistence without creating another customer. Legacy cases also seed a private old cache envelope at the new lookup key. All four modes retry the request, require the same minimal response, and check that only the original historical rental plus one newly created reservation exist. The legacy retry must be a cache hit. Two unit controls verify that quote and exact-vehicle bypass retain the incoming idempotency key and buffered body position.

The existing exact-vehicle replay test now checks the returned reservation identifier and verifies its promised vehicle in the database. Its vehicle-preservation guarantee is retained without depending on removed public response fields.

## Compatibility with the idempotency isolation PR

PR #463 changes generic cache key scope, body fingerprint validation and middleware placement. This privacy correction versions the response shape independently. Three-way patch checks succeeded in both application orders without manual conflict resolution. A disposable local worktree applying both changes passed its Release solution build, 58 focused middleware/controller unit tests, and 9 real API tests covering privacy, legacy cache replay/rate limits, quote replay and promised-vehicle preservation. No changes were applied to `main`.

## Review boundaries and risks

The public POST response intentionally removes fields. Consumers that previously relied on those internal fields must adopt the three-field contract; the repository checkout, hook, API tests and TypeScript build were checked. The correction does not change how customers are selected or how reservations, holds and payment state are persisted. No migration or dependency change is included.

Invalidating old generic cache entries means a legacy request without a quote that retries across the version cutover can execute again. The generic cache has no separate durable replay record for that case. Owners should account for outstanding legacy retries when scheduling a rollout. The repository checkout sends quote identifiers and retains its existing service-level replay proof across this change.

Known Pkcs/Xml dependency advisories and unrelated obsolete API warnings remain at the reviewed base. Local PostgreSQL 16.15 evidence does not replace PostgreSQL 17 CI. Current-head CI and owner review remain required. These tests cover the public response and related cache boundary; they do not certify the complete application's security.

No production data, real customer account, real payment provider, deployment or production migration was used. The PR must remain open for human review. No merge or automatic merge is authorized.
