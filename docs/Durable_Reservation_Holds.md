# Durable reservation hold lifecycle

Security finding: `reservation.redis-hold-expiry-divergence` (occurrence `occ_61506f9107224cbc01c9418f`).

## Invariant and implementation

Every newly committed stock-blocking `Hold` created through the reservation service has a durable `reservation_holds` row. Its `expires_at` is authoritative even when Redis keys expire, Redis is unavailable, or the worker restarts. The existing table is reused; no schema migration is required.

Creation joins the API's existing PostgreSQL transaction. Hold mutations serialize on the reservation row using `FOR UPDATE`; standalone operations own their transaction. Expiry reads current metadata inside that lock and atomically removes it with the eligible `Hold`/`Draft` status transition. Both automatic sweepers, including worker retry cleanup, use the same conditional expiry operation. A queued reservation ID cannot expire a renewed or extended live hold. A worker claim abandoned in `Processing` is reclaimable after a five-minute lease, so a crash between claiming and expiry cannot strand inventory indefinitely. Existing duplicate rows cannot defeat a later active hold.

Explicit release changes `Hold` to `Draft` and removes its metadata together, allowing another booking attempt and freeing inventory. Intentional cancellation changes and payment/progressed reservation states are preserved. Redis is a best-effort mirror and never determines validity or availability. Vehicle-cache cleanup compares ownership before deletion.

Guest reservations remain public. The existing 15-minute remaining-duration policy remains in force. `X-Session-Id` must be nonblank and at most 120 characters, matching the existing database field; oversized API inputs now receive an explicit rejection. Browser UUID sessions remain compatible.

## Deployment prerequisite for an existing installation

Old versions created healthy-Redis holds without durable metadata. They cannot safely be assigned an expiry based on reservation creation/update timestamps: old extensions only updated Redis. This change deliberately does not guess an expiry or automatically alter those historical reservations.

Before rolling out to an installation with old Redis-only holds:

1. Pause new hold creation and extensions on all old API instances, then allow at least the configured maximum remaining hold TTL (currently 15 minutes) to drain. Stop old workers before starting the new version; an old worker uses unconditional release semantics.
2. Identify `Hold` reservations without a `reservation_holds` row using the read-only query below. Reconcile each with booking/payment state through the normal administrative workflow. Missing Redis data alone is not evidence that an old reservation can be cancelled during the drain window.
3. Deploy API and worker together after reconciliation. Do not mix old and new writers or workers. Resume traffic and verify that new `Hold` transitions have durable expiry rows and that due rows become `Expired`.

```sql
SELECT r.id, r.status, r.updated_at
FROM reservations AS r
WHERE r.status = 'Hold'
  AND NOT EXISTS (
    SELECT 1 FROM reservation_holds AS h WHERE h.reservation_id = r.id
  );
```

This PR performs no deployment, data repair, production query, or production migration. Existing orphaned holds still require the operator reconciliation above.

## Validation coverage

- The healthy-Redis creation regression fails at the baseline because the durable row is missing.
- Unit coverage exercises durable discovery after Redis loss/restart, extension and recreation with stale expiry IDs, duplicates, explicit release, guest session exclusion, duration bounds, stale cache keys, cancellation and progressed-status preservation.
- PostgreSQL/Redis integration coverage exercises real TTL expiry through a restarted worker, successful insertion of an overlapping replacement reservation after expiry, API hold/release/recreate, rollback of an outer transaction, queued stale jobs, lock serialization across contexts, Redis key loss, and ownership-safe vehicle cache cleanup.
- Existing guest reservation, exact-vehicle booking, payment and worker suites remain applicable.
