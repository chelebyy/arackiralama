# Legacy idempotency response isolation

Review date: 17 October 2026. Base: `main` commit `1a352c984972a3c6a705b10073563eca470b92fc`.

## Verified problem

The generic idempotency middleware used a global Redis key derived only from the caller's `Idempotency-Key`. A cached response could be replayed across different endpoint paths, methods, sessions, query parameters or authenticated principals. A request with a different body could also receive the original response without validation. Cache hits ran before authentication, authorization and rate limiting.

The currently attributed endpoints are anonymous legacy reservation creation endpoints. This review does not claim a demonstrated authentication bypass on a protected endpoint. Quote-bearing and exact-vehicle creation already bypass the generic cache and use service-level replay validation.

Seven new negative regression cases failed before the correction. An identical-retry control passed, demonstrating that the tests exercised the existing response cache.

## Correction

- Derive a SHA-256 cache key from the idempotency key and the request's method, full path, query, content type, authenticated principal identity/type when present, and `X-Session-Id`.
- Store a SHA-256 fingerprint of the body with the cached response. Reusing the same scoped key with a different body returns HTTP 409 without disclosing the first response or executing a second operation.
- Use a new `idempotent:v2:` key namespace so old unscoped cached responses are never replayed.
- Run the middleware after authentication, authorization and rate limiting.
- Stop logging raw idempotency keys.
- Preserve quote/exact-vehicle service replay and reset buffered request bodies before the handler reads them.

An anonymous session header is caller-provided request context, not proof of authenticated identity. Clients without a session header retain legacy compatibility, with their request body still checked. Legacy replay compares exact body bytes; clients must resend the original payload. The quote path retains its existing semantic request checks.

## Validation

| Check | Result |
| --- | --- |
| New regression tests before correction | 7 failed; identical-retry control passed |
| Middleware, auth conventions and quote-persistence slice | 37 passed |
| Full backend unit suite | 889 passed, 0 failed, 0 skipped |
| Release solution build, including integration tests | Passed, 0 errors; 33 pre-existing warnings |
| Full API integration suite | 147 passed, 0 failed, against isolated local PostgreSQL 16.15 and Redis 7 |
| Changed-file whitespace verification and `git diff --check` | Passed |

The added real-pipeline regression creates one reservation, checks four identical cache replays, requires the sixth request to receive HTTP 429, and verifies that only one reservation exists. Local PostgreSQL 16 evidence does not replace the PostgreSQL 17 CI gate.

Relevant commands:

```bash
dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter 'FullyQualifiedName~IdempotencyMiddlewareTests|FullyQualifiedName~AuthEndpointSecurityConventionsTests|FullyQualifiedName~ReservationServiceQuotePersistenceTests'
dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-build
dotnet build backend/RentACar.sln -c Release
dotnet test backend/tests/RentACar.ApiIntegrationTests/RentACar.ApiIntegrationTests.csproj -c Release --no-build
```

## Boundaries

This correction isolates cached responses. It does not change the legacy cache's Redis-outage bypass behavior or add transactional exactly-once execution. The separate quote store remains responsible for atomic quote claims and replay.

Public creation response minimization is a separate change. Its response-version namespace must invalidate older creation responses regardless of which focused PR is integrated first. Existing cache entries expire naturally and are not deleted by this patch.

No merge, automatic merge, direct `main` push, shared-history rewrite, deployment, production migration, or existing PR closure is part of this work. The PR must remain open for human review.
