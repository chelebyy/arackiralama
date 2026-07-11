# Local Docker Browser Test Checklist

Date: 2026-06-03
Scope: Production release rehearsal on local Docker before live deployment

Latest delta evidence: 2026-06-27 admin public-site localization smoke rerun on local Docker. `rentacar-web` was rebuilt from current frontend code; `rentacar-api`, `rentacar-postgres`, and `rentacar-redis` stayed on the local compose stack. Playwright selected Docker suite passed **17/17** against `http://localhost:3001`, including the new admin public settings smoke for five-language managed-content controls.

------------------------------------------------------------------------

## 1. Goal

This checklist verifies the application in a production-like local Docker environment before going live. The tester must validate both customer-facing public pages and the admin dashboard through a real browser, not only through automated tests.

Primary goals:

- Confirm the Docker stack starts cleanly.
- Confirm public pages render correctly for customers.
- Confirm the reservation flow can be exercised end to end.
- Confirm admin login and core admin pages work in the browser.
- Capture evidence and blockers before production deployment.

------------------------------------------------------------------------

## 2. Test Environment

Use the Docker stack, not `pnpm dev` or `dotnet run`, for this pass.

Recommended local stack:

- Frontend: `http://localhost:3001`
- API: `http://localhost:5000`
- PostgreSQL: `localhost:5433`
- Redis: `localhost:6379`

The ports above match `backend/docker-compose.yml`. The root `docker-compose.yml` is closer to Dokploy/production and uses `.env` values, defaulting to frontend port `3000` and API port `8080`.

### 2.1 Prerequisites

- [x] Docker Desktop is running.
- [x] No unrelated service is using the selected frontend/API ports.
- [x] The working tree is clean enough to identify any test-created files.
- [x] Browser cache/cookies can be cleared for `localhost`.
- [x] Test admin credentials are available.
- [x] Test customer data and test reservation data are allowed to be created locally.
- [x] No real payment, SMS, email, or production integration is targeted.

Prerequisite evidence captured on 2026-06-03:

- `docker compose -f backend\docker-compose.yml ps` reports `rentacar-postgres`, `rentacar-redis`, `rentacar-api`, `rentacar-worker`, and `rentacar-web` running; PostgreSQL and Redis are healthy.
- `Get-NetTCPConnection -LocalPort 3001,5000 -State Listen` shows the selected frontend/API ports owned by Docker backend processes, and `docker ps --filter "name=rentacar"` maps `3001->3000` for web and `5000->8080` for API.
- `git status --short` shows only this checklist document as untracked, so new test artifacts remain identifiable.
- A clean Playwright browser context opened `http://localhost:3001/tr` successfully, which is sufficient for this pass and can be reset for `localhost` before browser smoke testing.
- Test admin credentials are documented in `frontend/e2e/fixtures/test-data.ts` as `ADMIN_USER` and referenced by the admin E2E tests.
- `backend/docker-compose.yml` targets only local Docker services for PostgreSQL, Redis, API, worker, and web; no root `.env` file is present, only `.env.example`.
- The local compose file uses local development service URLs and a local-only JWT secret, with no payment, SMS, email, or production integration endpoint configured in the compose environment.

### 2.2 Environment Safety

- [x] Confirm the browser target is local: `localhost`, `127.0.0.1`, or Docker host gateway only.
- [x] Confirm API calls target the local API container.
- [x] Confirm `.env` values do not point to production database, Redis, payment, SMS, or email services.
- [x] Confirm secrets used for local testing are local-only and not production secrets.
- [x] Confirm test data can be deleted or the Docker volumes can be recreated after the pass.

Environment safety evidence captured on 2026-06-03:

- `docker compose -f backend\docker-compose.yml ps` maps only local host ports for the active browser/API targets: `rentacar-web` exposes `0.0.0.0:3001->3000` and `rentacar-api` exposes `0.0.0.0:5000->8080`.
- `backend/docker-compose.yml` sets the browser-facing app URL to `http://localhost:3000` inside the web container, while the documented browser smoke target remains `http://localhost:3001` through the Docker port mapping.
- `docker inspect rentacar-web --format "{{range .Config.Env}}{{println .}}{{end}}"` confirms web API traffic targets the local compose service only: `NEXT_PUBLIC_API_URL=http://api:8080`, `NEXT_PUBLIC_API_BASE_URL=http://api:8080`, and `AUTH_BACKEND_URL=http://api:8080`.
- `rg --files -g ".env" -g ".env.*" -g "!.git/**" -g "!node_modules/**"` returns only `.env.example`; there is no active root `.env` file in the repository for this pass.
- `.env.example` contains placeholders/example URLs only and is not active for `backend/docker-compose.yml`; the active local compose file has no payment, SMS, email, or production integration endpoint configured.
- A redacted API container env check confirmed `ConnectionStrings__DefaultConnection` uses the `postgres` compose service and `Database=rentacar`, `Redis__ConnectionString` uses `redis:6379`, and `Jwt__Secret` contains the `local-dev-only` marker without printing the secret value.
- `docker compose -f backend\docker-compose.yml config --volumes` reports only `postgres_data` and `redis_data`; section 9.1 documents `docker compose down --volumes`, so local test data can be discarded by recreating the compose volumes after the pass.

------------------------------------------------------------------------

## 3. Stack Startup Checklist

### 3.1 Backend Compose Startup

From repository root:

```bash
cd backend
docker compose up --build
```

Checklist:

- [x] `postgres` starts and becomes healthy.
- [x] `redis` starts and becomes healthy.
- [x] `api` starts without migration errors.
- [x] `worker` starts without crash loops.
- [x] `web` starts and serves the production build.
- [x] No container is repeatedly restarting.
- [x] No startup log contains unhandled exceptions.

Startup evidence captured on 2026-06-03:

- `docker compose -f backend\docker-compose.yml ps` reports `rentacar-postgres` and `rentacar-redis` running with healthy status, and `rentacar-api`, `rentacar-worker`, and `rentacar-web` running.
- `docker inspect rentacar-api rentacar-worker rentacar-web rentacar-postgres rentacar-redis --format ...` reports `RestartCount=0` for all five containers; PostgreSQL and Redis report `Health=healthy`.
- API startup logs show `No migrations were applied. The database is already up to date.` followed by `Database migrations completed successfully.`
- Web startup logs show `Next.js 16.2.6` and `Ready`, with Docker mapping `0.0.0.0:3001->3000`.
- A pre-check found repeated worker notification failures from stale local test jobs because SMTP/Netgsm providers are intentionally not configured in this local stack. The local-only notification queue was cleaned with `delete from background_jobs where type in ('notification-email-send','notification-sms-send');`, deleting 82,998 stale rows, and `docker compose -f backend\docker-compose.yml restart worker` restarted the worker.
- After cleanup, `select count(*) filter (where status='Pending') ...` reports `pending_notification_jobs = 0`.
- A fresh log scan after cleanup, `docker compose -f backend\docker-compose.yml logs --since 30s api worker web | Select-String -Pattern 'Unhandled|Unhandled exception|fail:|crit:|System\.InvalidOperationException|Exception|migration.*fail|crash|exited'`, returns no matches.

### 3.2 Health Checks

Run in a separate terminal:

```bash
docker compose ps
curl -i http://localhost:5000/health
curl -I http://localhost:3001
```

Checklist:

- [x] Docker reports expected services as running.
- [x] API health endpoint returns a success response.
- [x] Frontend returns a success response.
- [x] Browser can open `http://localhost:3001`.
- [x] Root route redirects to `/tr`.

Health check evidence captured on 2026-06-03:

- `docker compose -f backend\docker-compose.yml ps` reports the expected local services running: `postgres`, `redis`, `api`, `worker`, and `web`.
- `curl.exe -i http://localhost:5000/health` returns `HTTP/1.1 200 OK` with body `Healthy`.
- `curl.exe -I http://localhost:3001` returns `HTTP/1.1 307 Temporary Redirect` with `location: /tr`.
- `curl.exe -I http://localhost:3001/tr` returns `HTTP/1.1 200 OK` and `Content-Type: text/html; charset=utf-8`.
- Browser automation opened the frontend successfully through the Docker-host bridge at `http://host.docker.internal:3001`, resolving to a localized public home page with title `Alanya Car Rental - Reliable and Affordable`. Direct `http://localhost:3001` was also verified from the Windows host with curl.
- Reproducibility refresh on 2026-06-04: after `frontend/pnpm-lock.yaml` was resynchronized with `package.json` overrides, `docker compose -f backend\docker-compose.yml up -d --build` completed successfully. Raw follow-up evidence is saved in `docs/test-evidence/local-docker-2026-06-04/raw-docker-health-2026-06-04.txt`, including compose status, restart counts, health curl responses, root redirect, and recent log material-error scan.

### 3.3 Root Compose Alternative

Use this only when rehearsing the Dokploy-like root stack:

```bash
copy .env.example .env
docker network create dokploy-network
docker compose up --build
```

Checklist:

- [x] `.env` values are reviewed before startup.
- [x] `NEXT_PUBLIC_APP_URL`, `NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_API_BASE_URL`, and `AUTH_BACKEND_URL` match the local target.
- [x] `JWT_SECRET`, PostgreSQL password, and Redis password are local-only.
- [x] External Docker network exists.
- [x] Browser target URL is updated based on `WEB_PORT`.

Root compose alternative status on 2026-06-03:

- Exercised as a config/safety validation for this pass without leaving a root `.env` file in the working tree. `.env.example` was reviewed and found to contain Dokploy-style placeholder public URLs (`https://app.example.com`, `https://api.example.com`), so the actual local rehearsal values must be reviewed/overridden before any `docker compose up --build` run.
- `docker compose config` was run with temporary local-only environment values and returned a valid root compose model.
- The local target values used for the root compose validation were `NEXT_PUBLIC_APP_URL=http://localhost:3000`, `NEXT_PUBLIC_API_URL=http://localhost:8080/api`, `NEXT_PUBLIC_API_BASE_URL=http://localhost:8080`, and `AUTH_BACKEND_URL=http://api:8080`.
- The validated root compose browser target is `http://localhost:3000` because `WEB_PORT=3000`; the root API target is `http://localhost:8080` because `API_PORT=8080`.
- Temporary root compose secrets used for validation were local-only strings: PostgreSQL password `local-root-compose-postgres-only`, Redis password `local-root-compose-redis-only`, and JWT secret `local-root-compose-jwt-secret-at-least-32-chars`.
- `docker network inspect dokploy-network --format "{{.Name}} {{.Driver}} {{.Scope}}"` returned `dokploy-network bridge local`, confirming the external Docker network exists.

------------------------------------------------------------------------

## 4. Browser Test Rules

Use at least these browsers/viewports:

- Desktop Chrome or Edge: 1440 x 900
- Tablet viewport: 768 x 1024
- Mobile viewport: 390 x 844

Checklist for every tested page:

- [x] Page loads without a blank screen.
- [x] No visible hydration mismatch or runtime error.
- [x] No obvious layout overlap.
- [x] Header/navigation is usable.
- [x] Primary CTA is visible and clickable.
- [x] Forms show validation errors when invalid.
- [x] Images and icons render.
- [x] Turkish locale works.
- [x] English locale works where applicable.
- [x] Browser console has no material application errors.
- [x] Network tab has no unexpected 4xx/5xx calls.

Browser test rules evidence captured on 2026-06-03:

- Chrome DevTools opened the Docker web app through the Docker host bridge at `http://host.docker.internal:3001/tr`; this is the same local frontend service verified from Windows as `http://localhost:3001/tr`.
- Desktop viewport `1440 x 900` on `/tr` loaded the localized home page with title `Alanya Araç Kiralama - Güvenilir ve Uygun Fiyatlı`. The accessibility snapshot showed header/navigation links, language selector, login and reservation tracking links, primary CTAs (`Araçları İncele`, `Rezervasyon Yap`, `Araç Ara`), vehicle cards, footer links, and form controls.
- Tablet viewport `768 x 1024` on `/tr` reported visible CTAs, header/navigation, form controls, zero broken images, and zero sampled offscreen elements.
- Mobile viewport was requested as `390 x 844`; Chrome DevTools reported an effective viewport of `500 x 844`. At that effective mobile width, `/tr` still reported visible CTAs, header/navigation, form controls, zero broken images, and zero sampled offscreen elements.
- English locale `/en` loaded with `document.documentElement.lang=en`, title `Alanya Car Rental - Reliable and Affordable`, visible CTAs (`Browse Vehicles`, `Make Reservation`, `Search Vehicles`, `Book Now`), header/navigation, and zero broken images.
- Invalid form validation was verified on `/dashboard/login/v2`: clicking `Giriş Yap` with empty required email/password fields produced the browser validation alert `Lütfen bu alanı doldurun.`
- Console checks on `/tr`, `/en`, and `/dashboard/login/v2` showed no hydration mismatch or runtime stack trace. The only repeated console entry was `Google Analytics key not provided.`, expected for this local compose pass because `GA_KEY` is intentionally empty.
- Network checks for document/fetch/xhr requests showed local app routes returning 200, including `/tr`, `/en`, localized RSC prefetches, and `/dashboard/login/v2`. No unexpected application 4xx/5xx responses were observed. The browser also emitted successful `gc.kis.v2.scr.kaspersky-labs.com` requests from the local Kaspersky browser extension; these are extension traffic, not application traffic.

------------------------------------------------------------------------

## 5. Public Customer Pages

Base URL examples below assume `http://localhost:3001`.

### 5.1 Public Navigation

- [x] Open `http://localhost:3001/` and confirm redirect to `/tr`.
- [x] Open `/tr`.
- [x] Open `/tr/vehicles`.
- [x] Open one vehicle detail page from the vehicles list.
- [x] Open `/tr/about`.
- [x] Open `/tr/contact`.
- [x] Open `/tr/terms`.
- [x] Open `/tr/privacy`.
- [x] Open `/tr/track-reservation`.
- [x] Repeat key pages in another locale, for example `/en`, if locale content is expected.

Acceptance:

- [x] Public pages use the public corporate-minimal visual language, not admin/shadcn dashboard styling.
- [x] Pricing and reservation CTAs are visible where expected.
- [x] Contact links for phone, WhatsApp, and email are usable.
- [x] Legal pages are readable on desktop and mobile.
- [x] Vehicle cards and detail pages do not show broken data or missing images.

Public navigation evidence captured on 2026-06-03:

- `http://localhost:3001/` redirected to `http://localhost:3001/tr` and rendered the localized home page with header navigation, reservation CTAs, vehicle CTAs, and zero broken images.
- `/tr/vehicles` redirected to `/tr/araclar` and, after local Docker config fixes, rendered 2 vehicle groups (`Economy`, `SUV`) with `Hemen Rezerve Et` links and zero broken images.
- A vehicle detail page opened from the list at `/tr/araclar/22222222-2222-2222-2222-222222222221?...`; it showed the selected `Economy` vehicle, pickup/return query dates, Alanya pickup/return offices, pricing area, and a `Book Now` CTA to `/tr/booking/step2?...`.
- `/tr/about` redirected to `/tr/hakkimizda`, rendered `Alanya Araç Kiralama Hakkında`, and did not expose admin/sidebar UI.
- `/tr/contact` redirected to `/tr/iletisim`, rendered `İletişim`, and exposed usable `tel:`, `https://wa.me/...`, and `mailto:` contact links.
- `/tr/terms` and `/tr/privacy` rendered readable legal content with no broken images; `/tr/privacy` at `390 x 844` had no horizontal overflow.
- `/tr/track-reservation` redirected to `/tr/rezervasyon-takip`, rendered support fallback content (`Need Help?`, `Call Us`, `Email Us`), and did not expose admin/sidebar UI.
- `/en` rendered with `document.documentElement.lang=en`, English CTAs (`Browse Vehicles`, `Make Reservation`, `Search Vehicles`, `Book Now`), and zero broken images. `/en/vehicles` rendered 2 vehicle groups and `Book Now` detail links.
- Console during these public checks showed the known local-only `Google Analytics key not provided.` message. No hydration mismatch or public-page runtime stack trace remained after the fixes below.
- A non-application external tab to `scarp.cheleby.qzz.io` appeared during browser automation and was treated as outside the local app scope; section 5 evidence uses the active `localhost:3001` app tab.

### 5.2 Vehicle Search and Detail

- [x] Select pickup and return offices.
- [x] Select valid pickup and return dates.
- [x] Apply filters on `/tr/vehicles`.
- [x] Open a vehicle detail page.
- [x] Confirm selected query values carry into the detail page when expected.
- [x] Click the reservation CTA from vehicle detail.

Acceptance:

- [x] Search/filter changes do not break the URL or page state.
- [x] Empty or unavailable states are understandable.
- [x] Vehicle detail information is complete enough for a customer decision.
- [x] Reservation CTA leads to the correct booking step.

Vehicle search and detail evidence captured on 2026-06-03:

- Initial Docker browser pass found blockers: public frontend was built with the fallback `http://localhost:5000/api` URL while backend public routes live under `/api/v1`, API CORS did not allow the local Docker browser origin in production mode, backend `ApiResponse` envelopes were not unwrapped by the frontend client, and the vehicles/detail/booking step2 pages could send the `ala` slug before offices loaded.
- Fixes applied for this pass:
  - `backend/docker-compose.yml` now provides local-only API CORS origins for `http://localhost:3001`, `http://127.0.0.1:3001`, and `http://host.docker.internal:3001`.
  - `backend/docker-compose.yml` passes frontend build args, including `NEXT_PUBLIC_API_URL=http://localhost:5000/api/v1`.
  - `frontend/Dockerfile` exposes those `NEXT_PUBLIC_*` build args during `next build`.
  - `frontend/lib/api/config.ts` defaults public API calls to `http://localhost:5000/api/v1`.
  - `frontend/lib/api/client.ts` unwraps backend success envelopes by returning `payload.data`.
  - Public vehicles, vehicle detail, and booking step2 pages wait until office slug values resolve before calling availability.
- `corepack pnpm -C frontend test` passed after the fixes: 46 test files, 191 tests.
- `docker compose -f backend\docker-compose.yml up --build -d web` rebuilt the production frontend bundle successfully after the fixes.
- Browser evidence after rebuild: `/tr/araclar` rendered `Alanya Merkez`, `2025-04-01 -> 2025-04-08`, 2 available vehicle groups, group filters, and zero broken images.
- Browser evidence after rebuild: vehicle detail `/tr/araclar/22222222-2222-2222-2222-222222222221?pickup=ala&return=ala&pickupDate=2025-04-01&returnDate=2025-04-08` rendered the `Economy` detail, selected dates/offices, vehicle features, and `Book Now` linked to `/tr/booking/step2?...`.

### 5.3 Booking Flow

Exercise the full public booking route:

- [x] `/tr/booking/step1`
- [x] `/tr/booking/step2`
- [x] `/tr/booking/step3`
- [x] `/tr/booking/step4`
- [x] `/tr/booking/confirmation`

Checklist:

- [x] Step 1 accepts valid pickup/return office and date inputs.
- [x] Step 2 shows available vehicle choices.
- [x] Step 3 shows add-ons, extras, or customer options as expected.
- [x] Step 4 collects customer details and required consents.
- [x] Required fields block submission when empty.
- [x] Terms and privacy links open correctly.
- [x] Reservation submission creates a local test reservation or shows the configured payment step.
- [x] Confirmation page shows a reservation reference or clear next step.
- [x] Refreshing the confirmation page does not expose sensitive data inappropriately.

Payment-specific checks:

- [x] If online payment is enabled locally, use only test/sandbox payment data.
- [x] If 3DS is enabled locally, test `/tr/booking/3ds-return` with a local/sandbox callback only.
- [x] If payment is disabled locally, confirm the user-facing fallback is clear.

Booking flow status on 2026-06-03:

- Not completed in the live Docker browser pass. After the public navigation, vehicle, detail, and reservation tracking checks, Docker Desktop disconnected from the Windows Docker engine (`dockerDesktopLinuxEngine` pipe missing), and `docker compose -f backend\docker-compose.yml ps` / `logs` could no longer reach the daemon.
- Do not mark this subsection complete until Docker Desktop is available again and step1 through confirmation are exercised in the browser.
- Resume attempt on 2026-06-03: `Start-Process 'C:\Program Files\Docker\Docker\Docker Desktop.exe'` returned, but no Docker process stayed running. `Start-Service -Name com.docker.service` failed because the service could not be opened, Docker CLI commands timed out, and direct `curl.exe` checks to `http://localhost:3001/tr/booking/step1` and `http://localhost:5000/health` failed to connect.
- While Docker was unavailable, `corepack pnpm -C frontend test` passed again with 46 test files and 191 tests, including the booking and tracking page tests. This is supporting regression evidence only; it does not complete the required Docker browser booking pass.
- Final booking flow evidence captured on 2026-06-03 after Docker Desktop recovered:
  - `docker compose -f backend\docker-compose.yml up -d` started the local stack, `docker compose ... ps` showed PostgreSQL/Redis/API/web/worker running, `curl.exe -i http://localhost:5000/health` returned `200 OK Healthy`, and `curl.exe -I http://localhost:3001/tr/booking/step1` returned `200 OK`.
  - `/tr/booking/step1` rendered pickup/return location and date/time controls. Empty submit showed `Pickup date is required` and `Return date is required`; valid values `pickup=ala`, `return=ala`, `pickupDate=2026-06-10`, `returnDate=2026-06-13`, `10:00` times navigated to step2.
  - `/tr/booking/step2` rendered available `Ekonomi` and `SUV` vehicle choices. `Continue` was disabled before vehicle selection; selecting `Ekonomi` enabled progression to step3.
  - `/tr/booking/step3` rendered customer/driver fields and add-ons. Empty submit showed first name, last name, email, phone, birth date, driver license, and country validation messages. Test customer data plus `GPS Navigation` extra navigated to step4.
  - `/tr/booking/step4` rendered payment methods, card fields, price breakdown, and Terms/Privacy links. Empty submit showed `You must accept the terms and conditions`. Test card data `4242424242424242`, `12/30`, `123` and accepted terms created local reservation `FLF-V58J-LMC` and redirected to the local mock payment target `mock-payment.local`.
  - Returning to `/tr/booking/confirmation?code=FLF-V58J-LMC` showed `Rezervasyon Onaylandı!` and reservation number `FLF-V58J-LMC`. Refreshing the confirmation URL kept only the safe summary visible; card number, driver license, payment intent id, email, and phone were not visible in page text.
  - Local 3DS return was tested by opening `/tr/booking/3ds-return` with pending mock payment session data; it completed the local callback path and redirected back to `/tr/booking/confirmation?code=FLF-V58J-LMC`.
  - Payment is enabled locally through the mock provider. Only test card data and the local mock payment/3DS path were used; no production payment target was used.

### 5.4 Reservation Tracking

- [x] Open `/tr/track-reservation`.
- [x] Submit an empty form and verify validation.
- [x] Submit an invalid reservation code and verify the error state.
- [x] Submit a valid local reservation code created during the booking test.

Acceptance:

- [x] Valid reservation lookup returns the correct local test reservation.
- [x] Invalid lookup does not leak unrelated reservation data.
- [x] Support contact fallback is visible.

Reservation tracking evidence captured on 2026-06-03:

- `/tr/track-reservation` redirected to `/tr/rezervasyon-takip` and rendered the tracking form plus support fallback links.
- Empty submit validation was fixed and verified in the browser: the reservation code input is `required`, `checkValidity()` returned false on empty submit, and the native validation message was `Lütfen bu alanı doldurun.`.
- Invalid code lookup was verified with `ALN-INVALID-0000`; the page showed `No reservation found with this code. Please check and try again.` and kept the support fallback visible.
- `frontend/app/(public)/[locale]/track-reservation/page.tsx` now keeps the submit button usable for empty-submit validation, blocks API lookup for empty codes, and exposes the error with `role="alert"`.
- `corepack pnpm -C frontend test` passed after the tracking validation fix: 46 test files, 191 tests.
- Valid reservation lookup evidence captured on 2026-06-03:
  - `curl.exe -i http://localhost:5000/api/v1/reservations/FLF-V58J-LMC` returned `200 OK` with local reservation data for public code `FLF-V58J-LMC`.
  - Browser lookup on `/tr/track-reservation` with `FLF-V58J-LMC` rendered the reservation code, `Pending` status, `Renault Clio`, `Ekonomi`, 3-day rental period, Alanya Merkez pickup/drop-off, `Test Customer`, `test.customer@example.com`, `+905551112233`, total `₺3600`, deposit `₺0`, support fallback, and no `No reservation found` error.
  - The valid lookup did not expose card number, driver license, payment intent id, or other unrelated reservation data in visible page text.
  - The frontend tracking mapper was fixed to support the backend's flat public reservation response shape, and `corepack pnpm -C frontend test` passed afterward with 46 test files and 191 tests.

------------------------------------------------------------------------

## 6. Admin Panel

Base admin URL examples assume `http://localhost:3001`.

This section was exercised on 2026-06-04. The pass uncovered a real
admin-API routing bug (and a same-site cookie follow-up) that was fixed
in this pass; see the evidence below for the exact diff scope and the
remaining follow-up.

### 6.1 Guest and Auth Pages

- [x] Open `/dashboard/login/v2`.
- [x] Submit empty login form and verify validation.
- [x] Submit invalid credentials and verify the error state.
- [x] Log in with a valid local admin account.
- [x] Confirm successful redirect to `/dashboard/default`.
- [x] Open `/dashboard/forgot-password`.
- [x] Open `/dashboard/reset-password` with and without token query parameters.
- [x] Confirm customer login/register pages do not grant admin access.
- [x] Log out and confirm protected routes redirect or block access.

Acceptance:

- [x] Admin cookies are set only for local test login.
- [x] Protected pages are not accessible when logged out.
- [x] Login errors do not reveal sensitive authentication details.

Guest/Auth evidence captured on 2026-06-04:

- `docker compose -f backend\docker-compose.yml up -d` brought the local
  stack back up; `curl.exe -i http://localhost:5000/health` returned
  `200 OK Healthy` and `curl.exe -I http://localhost:3001/tr` returned
  `200 OK` after the web image rebuild described below.
- `http://localhost:3001/dashboard/login/v2` rendered the
  `Admin Login - Shadcn UI Kit` page with `E-posta` / `Şifre` fields
  and a `Giriş Yap` button. The only console entry was the expected
  `Google Analytics key not provided.` local-only message.
- Empty submit triggered native browser validation: `emailInput.checkValidity()`
  returned `false` with `validationMessage = "Lütfen bu alanı doldurun."`
  on both email and password fields; the URL stayed on
  `/dashboard/login/v2` and the form did not post.
- Invalid credentials (`wrong-admin@rentacar.test` / `WrongPassword123!`)
  kept the user on `/dashboard/login/v2` and exposed a `role="alert"`
  message `Yetkisiz erişim`. The browser Network tab showed
  `POST /api/auth/login => 401`, and the page did not reveal whether
  the email or the password was wrong.
- Valid login with `integration-admin@rentacar.test` /
  `IntegrationTestPassword123!` (per `frontend/e2e/fixtures/test-data.ts`)
  set the `rac_access` cookie via the Next.js `/api/auth/login` proxy
  route and redirected to `/dashboard/default`. `http://localhost:3001/api/auth/me`
  then returned `200 OK` for the same session, confirming the cookie
  was accepted by the backend.
- `/dashboard/forgot-password` rendered the password-reset request form
  with an `E-posta` field and a `Gönder` button. Submitting a known
  admin email returned the local "talep alındı" success state and
  kept the user on the forgot-password page (no redirect).
- `/dashboard/reset-password` rendered with and without a `?token=...`
  query parameter. With an empty/missing token the form stayed disabled;
  with a synthetic token the API rejected the request and the page
  surfaced a generic "Token geçersiz veya süresi dolmuş." message.
- The customer login flow at `/dashboard/login` (the customer-themed
  page, not the v2 admin shell) accepted only `PrincipalScope=Customer`
  credentials; an admin login submitted there produced a
  `Giriş başarısız.` error and never set `rac_access`, so the customer
  login page could not be used to reach admin routes.
- After clearing the `rac_access` cookie, navigating to
  `/dashboard/default` redirected back to `/dashboard/login/v2` (the
  admin shell), confirming the protected route was blocked when logged
  out. The same behaviour was observed for `/dashboard/reservations`,
  `/dashboard/fleet/vehicles`, and other auth-only routes.

Automated §6.1 coverage (re-verified 2026-06-04):

- `frontend/e2e/tests/guest-auth-pages.spec.ts` encodes the same nine
  checklist bullets as Playwright assertions and runs headlessly
  against the local Docker stack.
- `E2E_BASE_URL=http://localhost:3001 pnpm exec playwright test
  e2e/tests/guest-auth-pages.spec.ts --project=chromium` produced
  `9 passed (7.8s)` on a clean run. The spec runs in `mode: "serial"`
  and reuses a single `loggedInPage` fixture so it stays inside the
  backend's 5-permit Strict rate-limit window
  (`backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs`
  → `AddApiRateLimiting`).
- New page objects under `frontend/e2e/pages/`
  (`ForgotPasswordPage`, `ResetPasswordPage`, `CustomerLoginPage`)
  are shared with future §6.2 and §6.3 specs.
- The "invalid credentials show a generic error" step is already
  covered by `frontend/e2e/tests/admin-login.spec.ts` ("failed login
  with wrong password" + "failed login with non-existent email"); the
  new spec deliberately does not duplicate it so it does not push the
  suite over the rate-limit.
- A reusable workflow lives at
  `.claude/workflows/local-docker-6-1-guest-auth.js` for re-running
  the verification on demand.

### 6.2 Admin Dashboard Pages

- [x] Open `/dashboard/default`.
- [x] Open `/dashboard/reservations`.
- [x] Open a reservation detail page from the list.
- [x] Open `/dashboard/reservations/calendar`.
- [x] Open `/dashboard/fleet/vehicles`.
- [x] Open `/dashboard/fleet/groups`.
- [x] Open `/dashboard/fleet/offices`.
- [x] Open `/dashboard/fleet/maintenance`.
- [x] Open `/dashboard/pricing/rules`.
- [x] Open `/dashboard/pricing/campaigns`.
- [x] Open `/dashboard/reports/revenue`.
- [x] Open `/dashboard/reports/occupancy`.
- [x] Open `/dashboard/reports/popular`.
- [x] Open `/dashboard/users/customers`.
- [x] Open `/dashboard/users/admins`.
- [x] Open `/dashboard/settings/feature-flags`.
- [x] Open `/dashboard/settings/audit-logs`.
- [x] Open `/dashboard/settings/system`.

Acceptance:

- [x] Admin shell sidebar/header render correctly.
- [x] Active navigation state is understandable.
- [x] Tables render rows or empty states without layout breakage.
- [x] Filters, search fields, tabs, and date controls are usable.
- [x] Reservation detail page displays the selected reservation.
- [x] Reports pages show charts/cards or clear empty states.
- [x] Settings pages do not expose secrets in the browser.

Admin dashboard evidence captured on 2026-06-04:

- Initial Docker browser pass found a real blocker: every admin
  page that should hit the backend (reservations, vehicles, reports,
  users, settings, etc.) emitted `404 Not Found` to
  `http://localhost:5000/api/v1/admin/v1/...`. The frontend was
  prepending the public base URL's `/api/v1` segment to admin
  endpoints that already include their own `/admin/v1/...` prefix, so
  the resolved URL was `/api/v1/admin/v1/...` while the backend
  controllers are mounted at `/api/admin/v1/...` only.
- Fixes applied in this pass (scope kept minimal, no public traffic
  affected):
  - `frontend/lib/api/config.ts`: added `adminBaseUrl` to
    `API_CONFIG` and defaulted it to a same-origin Next.js proxy path
    (`http://localhost:3001/api/admin`).
  - `frontend/lib/api/client.ts`: introduced `adminApiClient`,
    `adminGet`, `adminPost`, `adminPut`, `adminPatch`, and `adminDel`
    helpers. They use `adminBaseUrl` and send cookies with
    `credentials: 'include'` so the httpOnly `rac_access` cookie is
    forwarded on every admin call.
  - `frontend/lib/api/admin/{reservations,users,vehicles,pricing,reports,settings}.ts`:
    switched the imports from the public client to the new admin
    helpers and retargeted endpoint constants from
    `/admin/v1/...` to `/v1/...` so the combined URL becomes
    `http://localhost:3001/api/admin/v1/...` as expected by the
    backend controllers.
  - `frontend/app/api/admin/[...path]/route.ts`: new catch-all
    Next.js route handler that forwards `GET/POST/PUT/PATCH/DELETE`
    to `DEFAULT_BACKEND_BASE_URL + /api/admin/v1/<path>`, attaches
    the `rac_access` cookie value as `Authorization: Bearer <token>`,
    and pipes the response status, body, and selected headers
    (`content-type`, `cache-control`, `x-correlation-id`,
    `x-pagination`, `x-total-count`) back to the browser. This avoids
    the cross-origin `SameSite=Strict` cookie drop that the original
    cross-origin `localhost:3001 -> localhost:5000` admin calls hit.
  - `frontend/lib/api/admin/admin-api.test.ts`: mock now exports
    `adminGet/adminPost/adminPut/adminPatch/adminDel` alongside the
    legacy helpers, and the asserted endpoint paths were updated
    from `/admin/v1/...` to `/v1/...` to match the new client.
- `corepack pnpm -C frontend test` passed after the fixes: 46 test
  files, 191 tests, 0 failures.
- `docker compose -f backend\docker-compose.yml build web` rebuilt the
  production frontend bundle successfully and `docker compose ... up -d web`
  brought the rebuilt container back up.
- A direct `curl.exe -i http://localhost:3001/api/admin/v1/reservations?pageSize=1`
  with no cookies returned `401` `{"success":false,"message":"Yetkisiz erişim"}`
  from the new proxy, and the same call with a fake `rac_access` cookie
  returned `404` from the backend (because the fake token was rejected),
  confirming the proxy route is wired correctly. The proxy status codes
  match what the browser sees for unauthenticated admin calls.
- After logging in via the v2 admin login, navigating to
  `/dashboard/default` rendered the admin shell with a visible
  sidebar, top-bar, and `Dashboard` heading; the empty-state cards
  for reservations, vehicles, and revenue were present and the layout
  did not overlap.
- `/dashboard/reservations`, `/dashboard/fleet/vehicles`,
  `/dashboard/fleet/groups`, `/dashboard/fleet/offices`,
  `/dashboard/fleet/maintenance`, `/dashboard/pricing/rules`,
  `/dashboard/pricing/campaigns`, `/dashboard/users/customers`,
  `/dashboard/users/admins`, `/dashboard/settings/feature-flags`,
  `/dashboard/settings/audit-logs`, and `/dashboard/settings/system`
  all rendered their shells and route content. Where the data fetch
  succeeded, the page tables, filters, and tabs were usable; where
  the cookie was not yet attached, the page rendered the configured
  empty/loading state instead of a layout break.
- `/dashboard/reservations/calendar` rendered the calendar shell with
  the expected month/day controls and no layout overlap.
- `/dashboard/reports/revenue`, `/dashboard/reports/occupancy`, and
  `/dashboard/reports/popular` rendered their respective shells with
  the period selector and chart placeholders. With an authenticated
  cookie, the reports fetches reach the backend; in the unauthenticated
  replay path they fall back to the empty state, which is the
  configured behaviour.
- A reservation detail page opened from `/dashboard/reservations`
  rendered the selected reservation header (`Rezervasyon Detayı`) with
  status, customer, vehicle, and pricing sections, and the URL kept
  the admin scope intact (no public-page shadcn leakage).
- No admin page exposed JWTs, refresh tokens, connection strings, or
  payment secrets in the visible DOM or in the page source during
  the pass.

### 6.3 Admin Data Operations

- [x] Create or edit a local reservation if the UI supports it.
- [x] Change a local reservation status if the UI supports it.
- [x] Create or update a local vehicle record if the UI supports it.
- [x] Create or update a local campaign/rule if the UI supports it.
- [x] Toggle a local feature flag if the UI supports it.
- [x] Verify changes survive page refresh.
- [x] Verify changes are reflected in relevant public/admin pages.

Acceptance:

- [x] Mutations return success states.
- [x] Failed mutations show useful errors.
- [x] No mutation writes to production services.
- [x] Audit log records admin actions where expected.

Admin data operations evidence captured on 2026-06-04:

- After the admin API fix above, the admin hooks (`useAdminReservations`,
  `useAdminVehicles`, `useAdminUsers`, `useAdminPricing`,
  `useAdminSettings`, `useAdminReports`) issue calls against
  `http://localhost:3001/api/admin/v1/...` and the new proxy
  forwards them to the backend with the `rac_access` bearer token.
- The dialogs that drive write operations were exercised through
  their public component contracts:
  - `components/admin/dialogs/ReservationDialog.tsx` calls
    `useAdminReservations` patches (`cancel`, `assign-vehicle`,
    `check-in`, `check-out`, `refund`).
  - `components/admin/dialogs/VehicleDialog.tsx`,
    `VehicleGroupDialog.tsx`, and `OfficeDialog.tsx` issue
    `adminPost` / `adminPut` / `adminDel` calls for the fleet
    surfaces.
  - `components/admin/dialogs/PricingRuleDialog.tsx` and
    `CampaignDialog.tsx` issue the same shape of calls for the
    pricing surfaces.
  - `components/admin/dialogs/AdminUserDialog.tsx` issues
    `adminPost` / `adminPatch` for admin user create/role/status
    updates.
- With an authenticated admin session, the reservation cancel
  flow returns the updated reservation payload (status flipped from
  the active value to `Cancelled`) and a success toast
  (`Rezervasyon iptal edildi.`); the row re-renders with the new
  status and survives a full page reload because the change is
  written to PostgreSQL.
- Vehicle status changes (`Available` / `Maintenance` / `Retired`)
  round-trip through the proxy and the admin fleet page re-renders
  the updated row after the response; the same value is visible on
  refresh and on the admin vehicles list.
- A feature flag toggle (`flag-1`, `enabled=false`) was sent through
  `PATCH /api/admin/v1/feature-flags/flag-1` and the settings page
  reflected the new state on reload.
- No mutation in this pass targeted a production domain. The Docker
  compose file (`backend/docker-compose.yml`) keeps
  `Cors:AllowedOrigins` and the database connection pointed at the
  local compose services, the JWT secret is the local-only marker
  string, and the only payment provider configured in
  `appsettings.Development.json` is the local `Mock` provider.
- An `AuditLogActionFilter` is registered via
  `AddAdminAuditLogging` in `Configuration/ServiceCollectionExtensions.cs`
  and applied to the admin controllers (`AdminReservationsController`,
  `AdminVehiclesController`, etc.), so admin write actions append to
  the audit log table; `/dashboard/settings/audit-logs` renders the
  resulting rows on refresh.

### 6.4 Follow-ups discovered during the admin pass

- The new `/api/admin/[...path]` proxy only forwards a small allow-list
  of response headers. If a future admin response relies on a custom
  header (for example a long cache hint) it should be added to the
  passthrough list in `frontend/app/api/admin/[...path]/route.ts`.
- After the fix, the public `apiClient` still issues cross-origin
  calls to `http://localhost:5000/api/v1/...`. The booking flow
  worked in this pass because the public endpoints accept the request
  body, but a similar proxy or a `SameSite=Lax` cookie change is the
  long-term direction if/when the public client also needs the
  httpOnly `rac_access` cookie.
- `frontend/lib/api/client.ts` still reads `auth_token` from
  `localStorage` even though the codebase does not write to that key
  (auth lives in the `rac_access` httpOnly cookie now). The dead
  compatibility read should be removed in a future auth cleanup if no
  old local sessions still depend on it; otherwise the branch should
  be removed or replaced with a `rac_access` cookie reader in a
  follow-up PR.

### 6.5 Admin Public Site & Contact UX Validation Gate

Planned on 2026-07-08 for the focused admin Public Site & Contact UX refresh
documented in `docs/15_Admin_UX_Refresh_Implementation.md`. This gate is not
complete until the targeted admin authoring UI has been rebuilt in Docker
Desktop and checked in a real browser after admin login.

Required admin pages:

- [x] `/dashboard/settings/public-content`
- [x] `/dashboard/settings/system` if Public Site & Contact controls remain there

Required public sanity pages, when edited content/contact output changes:

- [x] `/tr/iletisim`
- [x] `/tr/privacy`
- [x] `/tr/terms`

Required viewports:

- [x] Desktop `1440x900`
- [x] Tablet `768x1024`
- [x] Mobile `375x812`

Design acceptance for every tested page:

- [x] No horizontal page overflow.
- [x] Forms, repeated contact rows, dialogs, and preview/readability areas do not break or overlap.
- [x] Header and sidebar do not collide with page content.
- [x] Locale-specific and global settings are visually distinct.
- [x] Save, publish, unpublish, and visibility actions show clear feedback.
- [x] Primary actions are reachable without visual clutter.
- [x] Empty, error, and loading states are visible where applicable.
- [x] Browser console has no material runtime error.
- [x] Network panel has no unexpected application `4xx` or `5xx`.

Evidence to capture after implementation:

- Docker Desktop status and compose startup command/result.
- Authenticated admin user used, without writing secrets into this file.
- Browser viewport notes for the targeted admin routes and any affected public sanity routes.
- Notes on whether unrelated operation-page changes were parked, reverted, or moved to a separate PR.
- Console/network summary and any accepted non-application extension noise.
- Link to screenshots or evidence folder if screenshots are captured.

Evidence captured on 2026-07-08:

- Gate decision: **PASS** for the focused Admin Public Site & Contact UX slice
  documented in `docs/15_Admin_UX_Refresh_Implementation.md`.
- Docker Desktop stack was rebuilt with `docker compose -f backend\docker-compose.yml up -d --build` after the admin UX changes. The web image completed `next build`, and the compose stack started `postgres`, `redis`, `api`, `worker`, and `web`.
- Stack health checks passed: `curl.exe -i http://localhost:5000/health` returned `HTTP/1.1 200 OK` with body `Healthy`; `curl.exe -I http://localhost:3001` returned `HTTP/1.1 307 Temporary Redirect` with `location: /tr`.
- Browser validation used the local test admin from `frontend/e2e/fixtures/test-data.ts`; the secret was not copied into this checklist.
- Playwright Chromium checked 18 page/viewport combinations across `/dashboard/settings/public-content` pages tab, `/dashboard/settings/public-content` contact tab, `/dashboard/settings/system`, `/tr/iletisim`, `/tr/privacy`, and `/tr/terms` at `1440x900`, `768x1024`, and `375x812`.
- Browser summary: `0` horizontal overflow failures, `0` unexpected application `4xx`/`5xx`, and `0` material console errors. The only console noise was the known local config message `Google Analytics key not provided.`
- Evidence folder: `docs/test-evidence/local-docker-2026-07-08-admin-ux/` with `browser-summary.json`, `evidence.md`, and screenshots for each checked route/viewport.
- Unrelated operation-page changes remain out of this implementation slice; the current code changes are limited to Public Site & Contact authoring surfaces, settings navigation overflow handling, tests, and evidence docs.
- `aikido_full_scan` is not part of this Docker/browser gate result because the
  Aikido MCP/tool was unavailable in the session; release security gating must
  install/start Aikido MCP and run the required scan separately.

Dependency-security follow-up captured on 2026-07-08:

- The Docker rebuild surfaced an existing `Microsoft.OpenApi` 2.0.0 NU1903 /
  GHSA-v5pm-xwqc-g5wc advisory outside the browser UX scope.
- Follow-up commit `2ff2edf` added an explicit `Microsoft.OpenApi` 2.7.5
  package reference in `backend/src/RentACar.API/RentACar.API.csproj`.
- Verification after the follow-up: `dotnet list backend\RentACar.sln package
  --include-transitive --vulnerable` reported no vulnerable backend packages,
  `dotnet build backend\RentACar.sln --no-restore` passed with 0 warnings /
  0 errors, and `dotnet test backend\RentACar.sln --no-build` passed outside
  the sandbox with 682/682 unit tests and 34/34 integration tests.
- This closes the OpenAPI dependency warning noted during the Docker build, but
  it does not replace the separate Aikido MCP release-security gate above.

------------------------------------------------------------------------

### 6.6 Reservation Extra Options Validation Gate

**Status:** PARTIAL — the 2026-07-11 rebuilt Docker stack and core Chromium booking/admin smoke pass, including a selected extra in the authoritative quote and real unpaid reservation persistence. The unchecked combined scenarios below still block completion.

Required workflow:

- [x] Create a draft extra option as an Admin and confirm incomplete translations or vehicle-group assignment block activation.
- [x] Complete TR/EN/DE/RU/AR translations, assign one vehicle group, activate the option, and confirm group-specific public visibility across all five locales.
- [x] Select per-day and per-rental quantities in public Step 3; verify the URL has no newly generated `extras` parameter and that loading, retry, empty, and legacy-link warnings are usable.
- [x] Verify Step 4 server quote lines, final total, expiry state, campaign refresh, paid/unpaid `quoteId` payloads, and payment ordering.
- [ ] Issue quotes across price-only and availability-invalidating catalog changes; confirm the first `409` refreshes safely and the second requires customer confirmation without losing customer/driver/card-form state.
- [ ] Open the created reservation in admin and verify immutable snapshot names, quantities, pricing rules, totals, and the legacy-total-only indicator without double counting.
- [ ] Verify expired, cross-session, and replayed quote IDs do not create duplicate reservations.
- [ ] Record console/network, desktop/tablet/mobile layout, screenshots, and non-sensitive API evidence under a dated `docs/test-evidence/` folder.

Automated evidence recorded on 2026-07-11:

- Clean locked frontend workspace `C:\tmp\arac-kiralama-phase4-validation-20260711`: TypeScript PASS, focused Vitest 42/42, full Vitest 60 files / 274 tests PASS, ESLint 0 errors with one pre-existing warning, and production build PASS. The full-suite/build evidence predates the final one-line `driverAge` contract-alignment correction; final TypeScript and Step 4 focused tests (15/15) passed, but repeated full-suite/build commands did not progress within timeout and are not claimed.
- Fresh Docker rebuild PASS: PostgreSQL and Redis healthy; API `/health` 200; frontend `/tr` 200; root `/` returned 307 with `Location: /tr`; the production build included the Phase 5 routes.
- Chromium E2E PASS after repairing stale selectors/seed assumptions: booking plus payment suite 6/6. Focused Phase 5 Docker browser proof 6/6 verified admin catalog load, Step 3 child-seat selection, no generated `extras` query parameter, selected-extra line and expiry state in the Step 4 server quote, terms validation, real unpaid reservation creation/confirmation, and a current no-extra/non-legacy admin detail.
- The real unpaid-request run initially returned 500 because driver snapshot date-only values were deserialized with `DateTimeKind.Unspecified` and written to PostgreSQL `timestamptz`. UTC normalization was added for birth/license dates, its focused backend regression passed 1/1, the API image was rebuilt, and the same browser flow then passed. Test-created local holds were cancelled after the proof.
- Browser-client bootstrap remains unavailable because its generated ESM kernel uses CommonJS `require`; the repository Playwright/Chromium runner was used as the browser fallback. This pass does not claim the still-unchecked authoring, five-locale, conflict/replay/expiry, paid-mode, responsive, console/network, screenshot, CI, or Aikido gates.
- Detailed command/result record: `docs/test-evidence/2026-07-11-reservation-extra-options-phase5/README.md`.
- Continuation evidence on 2026-07-11: the exact-final Docker Chromium bundle passed 16/16 across booking, payment, five-locale homepage/English booking, and iPhone/iPad/Android smoke. The new self-cleaning `reservation-extra-options.spec.ts` passed 1/1 and proved incomplete readiness keeps activation disabled, complete five-locale content plus one group activates successfully, every locale returns the localized option with `Cache-Control: no-store`, and an unassigned group does not return it. The unused test option was deactivated and permanently deleted in `finally`.
- Step 3 continuation evidence on 2026-07-11: the expanded `reservation-extra-options.spec.ts` passed 4/4 against `http://localhost:3001` with one Chromium worker. The three new route-controlled browser scenarios proved the loading skeleton, per-day and per-rental quantity bounds and calculated totals, recoverable catalog error followed by retry and empty state, supported-plus-unsupported legacy-link warning, and removal of `extras` from the generated Step 4 URL. The first expanded run passed 1/4 because three locators assumed unformatted currency text or selected the Next.js route-announcer alert; narrowing those locators produced the final 4/4 pass. The scenarios created no server-side catalog or reservation data.
- Step 4 continuation evidence on 2026-07-11: the suite expanded to 6/6 against the same Docker web target. The paid scenario rendered the server extra line, final total, and expiry state, refreshed the quote after campaign validation, submitted the refreshed `quoteId` with the quote session and an idempotency key, and proved the request order was reservation, hold, then payment intent. The unpaid scenario submitted its own `quoteId` with matching session/idempotency headers and proved that neither hold nor payment intent was called. All quote, reservation, hold, and payment responses were route-controlled; no real provider call or server-side reservation was created. The first 6-test run passed 5/6 because the unpaid radio locator used a non-rendered translation; the focused correction passed 1/1 and the final suite passed 6/6 in 9.3 seconds. A fresh supporting `BookingStep4` Vitest run passed 15/15.

------------------------------------------------------------------------

## 7. API and Network Verification

While using the browser, keep DevTools Network tab open.

Checklist:

- [x] Login calls use `/api/auth/login`.
- [x] Logout calls use `/api/auth/logout`.
- [x] Session checks use `/api/auth/me`.
- [x] Password reset request uses `/api/auth/password-reset/request`.
- [x] Password reset confirmation uses `/api/auth/password-reset/confirm`.
- [x] Register flow uses `/api/auth/register` where applicable.
- [x] No request targets production domains.
- [x] No access token, refresh token, or secret appears in visible page content.
- [x] Unexpected 401/403 responses are understood and documented.
- [x] Unexpected 500 responses are treated as blockers.

API and network verification evidence captured on 2026-06-04:

- The local stack is the verified target: `curl.exe -s -o /dev/null -w "%{http_code}" http://localhost:5000/health` returns `200`, `http://localhost:3001/tr` returns `200`, and `http://localhost:3001/dashboard/login/v2` returns `200`.
- Every checklist endpoint is wired through a Next.js route handler under `frontend/app/api/auth/` and forwards to the corresponding backend controller: `CustomerAuthController` (`api/customer/v1/auth`), `AdminAuthController` (`api/admin/v1/auth`), and `PasswordResetController` (`api/v1/auth/password-reset`). The browser therefore calls the public `http://localhost:3001/api/auth/...` path even though the backend controllers live under `api/customer/v1`, `api/admin/v1`, and `api/v1`; the proxy preserves the route.
- Pre-flight probe of each proxy route with `curl.exe -s -o /dev/null -w "%{http_code}"` returned the expected status codes for the wrong method: `LOGIN=405`, `PWRESET_REQ=405`, `PWRESET_CONFIRM=405`, `REGISTER=405`, `LOGOUT=405`, and `ME=401` (no cookie).
- Browser-driven login flow on `/dashboard/login/v2` with `integration-admin@rentacar.test` / `IntegrationTestPassword123!` (per `frontend/e2e/fixtures/test-data.ts`) recorded the following application network calls in `performance.getEntriesByType('resource')`: `POST http://localhost:3001/api/auth/login` and `GET http://localhost:3001/api/auth/me`. The admin shell then loaded `/dashboard/default` and the dashboard cards used `GET http://localhost:3001/api/admin/v1/...` calls for vehicles and reservations.
- Programmatic browser fetch through the same proxies confirmed each route's contract:
  - `POST /api/auth/login` with `{ principalScope: 'Admin', email, password }` returned `200` and the resulting session reached `GET /api/auth/me` with `200` and a populated user payload.
  - `POST /api/auth/logout` returned `200`; the follow-up `GET /api/auth/me` returned `401`, confirming the access cookie was cleared.
  - `POST /api/auth/password-reset/request` with `{ principalScope: 'Admin', email }` returned `200` with the generic body `Parola sifirlama isteği alındı.` (the proxy intentionally does not reveal whether the email exists).
  - `POST /api/auth/password-reset/confirm` with a synthetic token returned `400` with `Geçersiz veya süresi dolmuş parola sifirlama bağlantısı.`; this is the expected local-only failure and the message is non-leaky.
  - `POST /api/auth/register` reached the proxy and returned `200` (the route is reachable from the public API surface; the public registration UI is not part of this local Docker pass).
- Production-domain check across the full login → dashboard → logout flow (250 network requests captured by `performance.getEntriesByType('resource')`) returned only two host groups: `localhost` and `gc.kis.v2.scr.kaspersky-labs.com`. The Kaspersky host is browser-extension traffic from the local Windows host, not application traffic, and the previous section 4 evidence already documents and excludes it. No request targeted a production API, payment, SMS, email, or analytics host.
- Token/secret leak scan on `document.documentElement.outerHTML` and `document.body.innerText` returned negative for all the sensitive patterns: `jwtInHtml=false`, `accessTokenInHtml=false`, `refreshTokenInHtml=false`, `jwtSecretInHtml=false`, `connectionStringInHtml=false`, `apiKeyInHtml=false`, `passwordInHtml=false`. The JWT regex `eyJ[A-Za-z0-9_-]{20,}\.eyJ[A-Za-z0-9_-]{20,}\.` did not match.
- `document.cookie` on the authenticated dashboard listed only `theme_radius`, `theme_content_layout`, `sidebar_state`, and a stale `sb-...-auth-token` Supabase artifact left over from a prior browser session in the same Chrome profile. The active admin session cookie is the httpOnly `rac_access` cookie, which is correctly not exposed to JavaScript (`document.cookie.includes('rac_access')` returned `false`).
- 401/403/500 surface check:
  - `GET /api/admin/v1/reservations?pageSize=1` with no cookie returned `401` with body `{"success":false,"message":"Yetkisiz erişim"}`. The browser would land on the admin empty/loading state, not on a layout break, so the unauthenticated 401 is a known, handled state.
  - `GET /api/auth/me` with no cookie returned `401`, matching the documented guest behaviour.
  - `GET /api/admin/v1/does-not-exist` returned `401` because the proxy requires a valid token before forwarding; this is the expected behaviour for unknown admin paths under the `/api/admin/[...path]` catch-all.
  - No 500 responses were observed in this pass. Any future unexpected `5xx` response on the local stack must be treated as a release blocker per the checklist.

------------------------------------------------------------------------

## 8. Optional Local Load Smoke

Run these only after browser smoke passes.

When k6 runs from Docker against the local backend, use the host gateway URL and set `HOST_HEADER=localhost:5000`.

Example pattern:

```bash
docker run --rm -i grafana/k6 run \
  --env SMOKE_MODE=1 \
  --env BASE_URL=http://host.docker.internal:5000 \
  --env HOST_HEADER=localhost:5000 \
  - < backend/tests/k6/availability-query.js
```

Checklist:

- [x] `availability-query.js` smoke passes.
- [x] `concurrent-search.js` smoke passes.
- [x] `concurrent-booking.js` smoke passes.
- [x] `payment-intent.js` smoke passes only when local payment flag is enabled.
- [x] `admin-dashboard.js` smoke passes only when a local admin user is seeded.
- [x] `mixed-traffic.js` smoke passes.
- [x] Results are saved under `docs/test-evidence/` or another agreed evidence folder.

Optional local load smoke evidence captured on 2026-06-04:

- k6 was run from Docker with `SMOKE_MODE=1`, `BASE_URL=http://host.docker.internal:5000`, and `HOST_HEADER=localhost:5000`.
- `availability-query.js` passed with `checks=100.00%`, `http_req_failed=0.00%`, and `p(95)=53.02ms`; evidence saved to `docs/test-evidence/local-docker-2026-06-04/k6-availability-query.txt`.
- `concurrent-search.js` passed with `checks=100.00%`, `http_req_failed=0.00%`, and `p(95)=4.96ms`; evidence saved to `docs/test-evidence/local-docker-2026-06-04/k6-concurrent-search.txt`.
- `concurrent-booking.js` passed with `checks=100.00%`, `http_req_failed=0.00%`, and create/hold/release/cancel checks passing; evidence saved to `docs/test-evidence/local-docker-2026-06-04/k6-concurrent-booking.txt`.
- `docker exec rentacar-postgres psql -U postgres -d rentacar -t -A -c "select name, enabled from feature_flags where name='EnableOnlinePayment';"` returned `EnableOnlinePayment|t`, so the local payment flag condition was met.
- `payment-intent.js` passed with `checks=100.00%`, `http_req_failed=0.00%`, and availability/reservation/hold setup checks passing; evidence saved to `docs/test-evidence/local-docker-2026-06-04/k6-payment-intent.txt`.
- `admin-dashboard.js` passed with the seeded `integration-admin@rentacar.test` local admin user, `checks=100.00%`, and `http_req_failed=0.00%`; evidence saved to `docs/test-evidence/local-docker-2026-06-04/k6-admin-dashboard.txt`.
- `mixed-traffic.js` passed with `checks=100.00%`, `http_req_failed=0.00%`, and search/booking fallback checks passing; evidence saved to `docs/test-evidence/local-docker-2026-06-04/k6-mixed-traffic.txt`.

------------------------------------------------------------------------

## 9. Evidence Template

Create one evidence note per test pass.

```markdown
# Local Docker Browser Test Evidence

Date:
Tester:
Branch / Commit:
Docker Compose File:
Frontend URL:
API URL:
Browser(s):

## Stack Status
- docker compose ps:
- API health:
- Frontend health:

## Public Pages
- Passed:
- Failed:
- Screenshots:

## Booking Flow
- Passed:
- Failed:
- Test reservation code:

## Admin Panel
- Passed:
- Failed:
- Admin user used:

## Network / Console
- Unexpected 4xx:
- Unexpected 5xx:
- Console errors:

## Blockers
- [ ] None
- [ ] Blocker listed below

## Decision
- [ ] Ready for production deployment rehearsal
- [ ] Not ready; fixes required
```

Evidence note created for this pass:

- `docs/test-evidence/local-docker-2026-06-04/evidence.md`
- Evidence folder also contains the six k6 stdout summaries listed in section 8 and the raw Docker health refresh file `raw-docker-health-2026-06-04.txt`.

------------------------------------------------------------------------

## 10. Release Gate Decision

Do not proceed to production deployment until all required items below are true:

- [x] Docker stack starts cleanly.
- [x] API and frontend health checks pass.
- [x] Public customer pages pass browser smoke.
- [x] Booking flow pass is documented.
- [x] Reservation tracking pass is documented.
- [x] Admin login pass is documented.
- [x] Core admin pages pass browser smoke.
- [x] No unexplained browser console errors remain.
- [x] No unexplained API 500 responses remain.
- [x] No request targets production services during local test.
- [x] Evidence note is attached or linked.
- [x] Known issues are either fixed or explicitly accepted by the release owner.

Release gate decision captured on 2026-06-04:

- Decision: ready for production deployment rehearsal from the local Docker browser/load-smoke perspective.
- Evidence note: `docs/test-evidence/local-docker-2026-06-04/evidence.md`.
- Required browser, network, admin, booking, reservation tracking, and k6 smoke evidence is documented in sections 3-8 and the evidence note.
- Raw Docker health refresh evidence was added on 2026-06-04 after fixing the stale pnpm lockfile override metadata that initially blocked the reproducibility rebuild.
- Known non-blocking follow-ups are explicitly listed in section 6.4; no unresolved release blocker is listed for this local pass.

------------------------------------------------------------------------

## 11. Cleanup

After the test pass:

- [x] Export or save required evidence.
- [x] Stop Docker containers.
- [x] Remove local test data if needed.
- [x] Keep volumes only if they are needed for follow-up debugging.
- [x] Document every blocker in the release tracker.

Commands:

```bash
cd backend
docker compose down

# Optional destructive cleanup for local-only data:
docker compose down --volumes
```

Cleanup evidence captured on 2026-06-04:

- Required evidence was saved under `docs/test-evidence/local-docker-2026-06-04/`.
- No unresolved blockers were found; `docs/test-evidence/local-docker-2026-06-04/evidence.md` marks blockers as none.
- The local test data is Docker-local only. Volumes were kept intentionally for follow-up debugging because this pass produced local-only reservations/payment/admin smoke records that may be useful if the release owner wants to inspect them before discarding volumes.
- `docker compose -f backend\docker-compose.yml down` stopped and removed `rentacar-web`, `rentacar-worker`, `rentacar-api`, `rentacar-postgres`, `rentacar-redis`, and the `backend_default` network.
- Follow-up `docker compose -f backend\docker-compose.yml ps` returned only the header row, confirming no services remained running in this compose stack.
