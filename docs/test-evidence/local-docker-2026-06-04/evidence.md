# Local Docker Browser Test Evidence

Date: 2026-06-04
Tester: Codex
Branch / Commit: feat/phase10-public-page-coverage / 82510bd9a4c9baf071ea3584057d6dff85e329ec
Docker Compose File: backend/docker-compose.yml
Frontend URL: http://localhost:3001
API URL: http://localhost:5000
Browser(s): Chrome/Edge browser automation evidence captured in docs/13_Local_Docker_Browser_Test_Checklist.md

## Stack Status
- docker compose ps: rentacar-api, rentacar-web, rentacar-worker, rentacar-postgres, and rentacar-redis running; PostgreSQL and Redis healthy.
- API health: `curl.exe -s -o NUL -w "API=%{http_code}" http://localhost:5000/health` returned `API=200`.
- Frontend health: `curl.exe -s -o NUL -w "FRONTEND=%{http_code}" http://localhost:3001/tr` returned `FRONTEND=200`.
- Raw refresh evidence: `raw-docker-health-2026-06-04.txt` captures the 2026-06-04 reproducibility rebuild and live health refresh. The first rebuild attempt exposed a stale `pnpm-lock.yaml` override mismatch; after `corepack pnpm -C frontend install --lockfile-only`, `docker compose -f backend\docker-compose.yml up -d --build` completed and the raw file records compose status, restart counts, API/frontend curl responses, root redirect, and a clean recent material-error log scan.
- Cleanup refresh: after the 2026-06-04 reproducibility check, `docker compose -f backend\docker-compose.yml down` completed and the follow-up `ps` output returned only the header row.

## Public Pages
- Passed: Public home, vehicles, vehicle detail, about, contact, terms, privacy, reservation tracking, and English locale smoke are documented in checklist sections 4 and 5.
- Failed: None unresolved for this pass.
- Screenshots: Browser automation evidence is documented in checklist sections 4 and 5; no new screenshot artifact was required for sections 8-11.

## Booking Flow
- Passed: Booking steps 1-4 and confirmation pass are documented in checklist section 5.3. k6 booking smoke also passed in `k6-concurrent-booking.txt`.
- Failed: None unresolved for this pass.
- Test reservation code: Local-only browser and k6 reservations were created in Docker PostgreSQL; k6 cleanup cancelled/released its smoke reservations where scripted.

## Admin Panel
- Passed: Admin login, core admin pages, admin data operations, admin network proxy, and admin-dashboard k6 smoke are documented in checklist sections 6 and 8.
- Failed: None unresolved for this pass.
- Admin user used: integration-admin@rentacar.test

## Network / Console
- Unexpected 4xx: No unresolved unexpected 4xx remained. Known unauthenticated 401 responses are documented in checklist section 7.
- Unexpected 5xx: None observed in the documented pass.
- Console errors: No material hydration/runtime console errors remained; the local-only missing GA key message is documented as expected.

## Local Load Smoke
- availability-query.js: Passed, see `k6-availability-query.txt`.
- concurrent-search.js: Passed, see `k6-concurrent-search.txt`.
- concurrent-booking.js: Passed, see `k6-concurrent-booking.txt`.
- payment-intent.js: Passed, see `k6-payment-intent.txt`; local DB flag `EnableOnlinePayment` was enabled.
- admin-dashboard.js: Passed, see `k6-admin-dashboard.txt`; seeded local admin user was available.
- mixed-traffic.js: Passed, see `k6-mixed-traffic.txt`.
- Docker health refresh: Passed, see `raw-docker-health-2026-06-04.txt`.
- Frontend regression refresh: `corepack pnpm -C frontend test` passed with 46 test files and 191 tests after the lockfile and `ReservationTimeline` test harness refresh.

## Blockers
- [x] None
- [ ] Blocker listed below

## Decision
- [x] Ready for production deployment rehearsal
- [ ] Not ready; fixes required

Known issues accepted for this local release gate are listed in checklist section 6.4 as follow-ups, not blockers.
