# PR #235 — fix(phase10): close local docker 100-user load baseline

> **Bu dosya, gh auth sonrası `gh pr create --body-file` ile kullanılmak üzere hazırlanmıştır.**

## Title

```
fix(phase10): close local docker 100-user load baseline
```

## Head / Base

- **head:** `feat/phase10-public-page-coverage`
- **base:** `main`
- **commits in this PR (since merge of #234):**
  - `382dd09` fix(test): align rate limiting reflection test
  - `28da0ae` fix(phase10): move concurrent booking seed to startup
  - `8a90b70` fix(phase10): address load-baseline review follow-up
  - `ce167b7` merge: origin/main into feat/phase10-public-page-coverage
  - (plus pre-#234 load-baseline closure chain merged in via #234)

## Summary

Phase 10.4 local Docker load validation is now **green** for the 100-user concurrent
booking baseline. The last failing piece was a rare `reservations_no_overlap`
race during hold creation under contention. The fix keeps the exclusion
constraint intact and retries the hold save against the next vehicle candidate
when a rare overlap violation is thrown. Inventory starvation was solved by a
local-only startup seed (120 additional economy vehicles) gated on
`RateLimiting:LoadTestSessionPartition` so production behavior is unchanged.

## Verification Evidence (Local Docker)

```
dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj \
  --no-restore --filter "FullyQualifiedName~ReservationServiceTests"
  → 67/67 PASS

dotnet build backend/src/RentACar.Infrastructure/RentACar.Infrastructure.csproj \
  --no-restore
  → 0 warning / 0 error

docker compose up -d --build api

docker run --rm -w /scripts \
  -e BASE_URL=http://host.docker.internal:5000 \
  -e HOST_HEADER=localhost:5000 \
  -e SMOKE_MODE=0 \
  -v "C:/All_Project/Araç Kiralama/backend/tests/k6:/scripts" \
  grafana/k6:latest run /scripts/concurrent-booking.js
```

Final k6 summary:

| Metric | Value |
|---|---|
| `search status` | 200 |
| `create status` | 201 or 200 |
| `hold status` | 200 |
| `release status` | 200 or 204 |
| `cancel status` | 200 |
| `http_req_failed` | **0.00%** |
| `http_req_duration p95` | **16.87 ms** |
| iterations | **9686** |

## Files Changed

### Backend (12 files, +502 / −98)

- `backend/docker-compose.yml` — enable `RateLimiting:LoadTestSessionPartition` for local compose
- `backend/src/RentACar.API/Configuration/ApplicationBuilderExtensions.cs` — invoke the local inventory seed at startup
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs` — session-aware rate limiter partition
- `backend/src/RentACar.API/Services/ReservationService.cs` — load-test-aware candidate ordering + overlap-violation retry
- `backend/src/RentACar.API/appsettings.json` — default-off load-test partition flag
- `backend/src/RentACar.Infrastructure/Data/ConcurrentBookingInventorySeedExtensions.cs` — **new** local startup inventory seed (120 economy vehicles)
- `backend/src/RentACar.Infrastructure/Data/Migrations/20260517222000_AddConcurrentBookingVehicleSeed.cs` — no-op migration shell
- `backend/src/RentACar.Infrastructure/RentACar.Infrastructure.csproj` — add `Microsoft.Extensions.Configuration.Binder` (PR review follow-up)
- `backend/tests/RentACar.Tests/Unit/Services/AuthEndpointSecurityConventionsTests.cs` — align rate limiting reflection test
- `backend/tests/RentACar.Tests/Unit/Services/ReservationServiceTests.cs` — overlap-retry path coverage
- `backend/tests/k6/README.md` — document the load-test assumptions
- `backend/tests/k6/concurrent-booking.js` — 100-user baseline + cleanup

### Docs (6 files, +191 / −6)

- `docs/02_ADR_ENTERPRISE_FULL.md` — record the verified 100-user baseline
- `docs/04_IDD_ENTERPRISE_FULL.md` — record the local-Docker validation pattern
- `docs/09_Implementation_Plan.md` — mark concurrent booking baseline complete
- `docs/10_Execution_Tracking.md` — log the load-baseline closure delivery
- `docs/12_Phase10_PreLaunch_Gates.md` — gate #9 → ✅ GO (LOCAL DOCKER BASELINE VERIFIED 18 May 2026)
- `docs/handoffs/2026-05-18-022152-phase10-load-baseline-complete-and-docs-sync.md` — new session handoff

## Pre-Launch Gate Impact

Gate #9 in `docs/12_Phase10_PreLaunch_Gates.md` moves from partial to **GO**:

> ✅ **LOCAL DOCKER BASELINE VERIFIED 18 May 2026** — booking flow passed locally in
> Docker after local startup inventory seed expansion, load-test session partitioning,
> and overlap-retry stabilization. Final k6 baseline completed with
> `http_req_failed 0.00%`, `http_req_duration p95 16.87ms`, and `9686` iterations.

## Out of Scope (intentionally NOT in this PR)

- Dokploy reruns (deferred until deployed infrastructure is available)
- Performance, monitoring, UAT, rollback-plan, incident-response gates (still DEFERRED)
- `.sisyphus/` and `backend/tests/k6/results/` (untracked; not staged)
- The 5 historical handoff files deleted in the working tree (per handoff instructions,
  intentionally not staged for this PR)

## Local Repro

```bash
# 1. Build
dotnet build backend/RentACar.sln

# 2. Run unit + integration coverage
dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj \
  --no-restore --filter "FullyQualifiedName~ReservationServiceTests"

# 3. Local Docker stack
cd backend
docker compose up -d --build api
docker compose ps

# 4. Run k6 baseline
docker run --rm -w /scripts \
  -e BASE_URL=http://host.docker.internal:5000 \
  -e HOST_HEADER=localhost:5000 \
  -e SMOKE_MODE=0 \
  -v "$(pwd)/tests/k6:/scripts" \
  grafana/k6:latest run /scripts/concurrent-booking.js
```

## Related

- Handoff: `docs/handoffs/2026-05-18-022152-phase10-load-baseline-complete-and-docs-sync.md`
- Pre-Launch Gates: `docs/12_Phase10_PreLaunch_Gates.md` (gate #9)
- Previous PRs in this branch: #234 (docs verify), #233 (stabilize)
- Migration shell: `backend/src/RentACar.Infrastructure/Data/Migrations/20260517222000_AddConcurrentBookingVehicleSeed.cs`

## gh auth sonrası uygulanacak komut

```bash
gh auth login -h github.com
gh pr create \
  --base main \
  --head feat/phase10-public-page-coverage \
  --title "fix(phase10): close local docker 100-user load baseline" \
  --body-file docs/handoffs/2026-05-18-PR-235-load-baseline-closure-body.md
```
