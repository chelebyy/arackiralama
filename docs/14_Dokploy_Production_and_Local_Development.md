# Dokploy Production and Local Development Runbook

Date: 2026-06-09
Updated: 2026-07-17 (public membership surface disabled)

## What Happened in the Dokploy Demo

The demo site is served from:

- Web/domain: `https://arac.cheleby.qzz.io`
- Internal Docker API target from the web container: `http://api:8080`
- Public browser API path: `https://arac.cheleby.qzz.io/api/v1/...`
- Public uploaded media path: `https://arac.cheleby.qzz.io/uploads/...`

The important production lesson is that browser-facing URLs must not point to a Tailscale or Docker-internal address. The public frontend bundle previously contained:

```text
http://100.122.228.27:8080/api/v1
```

That worked only from a Tailscale-accessible environment and broke for normal visitors on the Cloudflare domain. The fix was to use same-origin public routes through the web service:

- `frontend/app/api/v1/[...path]/route.ts`
- `frontend/app/uploads/[...path]/route.ts`

These routes proxy public API and uploaded media requests to `AUTH_BACKEND_URL`.

## Production Settings

For a real single-domain production deployment, keep this shape:

```env
NEXT_PUBLIC_APP_URL=https://your-domain.com
NEXT_PUBLIC_API_BASE_URL=https://your-domain.com
NEXT_PUBLIC_API_URL=https://your-domain.com/api/v1
NEXT_PUBLIC_ADMIN_API_URL=/api/admin
NOTIFICATIONS_PUBLIC_FRONTEND_BASE_URL=https://your-domain.com
AUTH_BACKEND_URL=http://api:8080
PAYMENT_PROVIDER=Disabled
PAYMENT_CURRENCY=TRY
PAYMENT_ENABLE_PAYMENTS=false
```

Rules:

- `NEXT_PUBLIC_*` values are browser-facing and are baked into the Next.js client bundle at build time.
- `NOTIFICATIONS_PUBLIC_FRONTEND_BASE_URL` remains a server-side trusted HTTPS origin for the retained notification/claim implementation, but the current public registration and claim surfaces return `404`. Keeping this value configured does not reopen membership or prove email delivery; any future re-enablement requires a separate product and security decision.
- `AUTH_BACKEND_URL` is server-side only and should stay internal in Dokploy/Docker: `http://api:8080`.
- After changing any `NEXT_PUBLIC_*` value in Dokploy, rebuild/redeploy the web service.
- Do not use Tailscale IPs or Docker service names in browser-facing `NEXT_PUBLIC_*` values for production.
- The root `docker-compose.yml` is the Dokploy/production compose file and expects Dokploy's external network.
- `PAYMENT_PROVIDER=Disabled` is the explicit fail-closed mode for the test/demo site. It is valid only with `PAYMENT_ENABLE_PAYMENTS=false`, does not require synthetic provider credentials, and never falls back to Mock.
- Do not set `PAYMENT_ENABLE_PAYMENTS=true`. The current Iyzico implementation is simulated and is not an authorization source for real payments.

Production hardening before real launch:

- Disable demo local admin seed:
  - `LOCAL_ADMIN_SEED_ENABLED=false`
  - `LOCAL_ADMIN_SEED_ALLOW_OUTSIDE_DEVELOPMENT=false`
  - clear `LOCAL_ADMIN_SEED_PASSWORD`
- Rotate any API keys or secrets that were shared during setup.
- Replace all placeholder passwords and JWT secrets.
- Confirm `ALLOWED_HOSTS` includes the public domain and expected internal names.
- Set a real `GA_KEY` only if analytics is intentionally enabled.
- Keep database backup/export procedures outside Git.
- Keep `PAYMENT_PROVIDER=Disabled` and `PAYMENT_ENABLE_PAYMENTS=false` until a real provider integration, callback verification, mismatch/replay tests, and controlled provider sandbox proof have passed.

## Dokploy Deployment Checklist

1. Deploy from `main`.
2. Use compose file path:

   ```text
   docker-compose.yml
   ```

3. Attach the public domain to the `web` service.
4. Set env values using the production settings above, including retained `NOTIFICATIONS_PUBLIC_FRONTEND_BASE_URL=https://your-domain.com`, `PAYMENT_PROVIDER=Disabled`, and `PAYMENT_ENABLE_PAYMENTS=false`. The notification origin is configuration hygiene only and does not enable the disabled membership routes.
5. Trigger deploy and wait for status `done`.
6. Verify:

   ```text
   https://your-domain.com
   https://your-domain.com/api/v1/vehicles
   https://your-domain.com/uploads/...
   ```

## Latest Demo Deployment Evidence (17 July 2026)

- PR #410 head `0e91b8d423977d1680bd29820eba1a75a80d6477` was squash-merged to `main` as `d0a7990bad1b7847edd4439e670f4dcfc8321a71`; the exact merge commit passed CI, Docker build/push, CodeQL, Secret Scan, and React Doctor.
- The operator reported the Dokploy Compose deployment successful. The deployed commit is inferred from the current `main` head and observed Disabled-mode behavior; Dokploy container metadata/logs were not independently read in this session.
- A short warm-up interval produced transient `500` responses for the DB-backed vehicles/settings endpoints. The final cache-busted matrix returned `200` for `/`, `/tr`, `/api/v1/vehicles`, and `/api/v1/public-site-settings`.
- Public settings reported credit card, debit card, and PayPal disabled; unpaid reservation request enabled; and `anyEnabled=true`.
- Zero-ID/no-write POST probes to payment intent, 3DS return, and webhook entry points each returned `503` with the disabled-payment response before identifier/provider validation.
- This proves the demo deployment is available with public payment processing contained. It is not real-provider payment-integrity evidence or a production-launch approval.

## Email Scope Decision (17 July 2026)

- Public customer membership/account claim is not part of the intended current product or release scope. The current source now removes the public login/registration entry points and returns `404` from registration/claim pages, frontend proxies, and backend endpoints before side effects. Existing customers may still use the direct login route.
- Future automatic email is intended for reservation lifecycle notifications. The provider and exact trigger/event matrix have not been selected, so this runbook does not claim that reservation emails are currently delivered.
- `NOTIFICATIONS_PUBLIC_FRONTEND_BASE_URL` remains a trusted-origin guard for retained internal code. Setting it does not enable membership, configure an email provider, or prove delivery.
- PR #413 head `5039c6028f1c21c8bd5aaecbb1cb3cc5e996ccee` was squash-merged as `fb7ca83e01599556ea9b06d24d9c570a4d0a111b`; post-merge CI/security and the GHCR push succeeded. The live site did not advance from the earlier behavior until the operator used the Dokploy Compose **Deploy** action; the completed deployment reported that exact merge commit.
- Cache-bypassed production HTTP and real Chromium checks then returned `404` for all five localized claim pages, `/dashboard/register/v1`, and the two public proxy endpoints; `/dashboard/login/v1` remained directly reachable with `200`, and the public homepage exposed no login link. Empty JSON proxy probes did not mutate production data. The source/local/deployed-public membership gate is closed. Direct internal-backend exact/case/trailing-slash runtime proof, container metadata/logs, production DB/job counts, and the other original attack paths remain separate unreviewed or open evidence gates.
- This change adds no credentials and does not select an email provider.

## Local Development: Recommended Daily Flow

Use `backend/docker-compose.yml` for local development, not the root production compose.

From the repository root:

```powershell
docker compose -f backend/docker-compose.yml up --build postgres redis api worker
```

Then run the frontend as a dev server on port `3001`:

```powershell
corepack pnpm -C frontend install
corepack pnpm -C frontend dev -- -p 3001
```

Local URLs:

- Frontend: `http://localhost:3001`
- API: `http://localhost:5000`
- API health: `http://localhost:5000/health`
- PostgreSQL host port: `5433`
- Redis host port: `6379`

Admin seed for local development:

```text
Email: integration-admin@rentacar.test
Password: IntegrationTestPassword123!
```

If frontend auth/API calls need explicit local env values, create `frontend/.env.local`:

```env
NEXT_PUBLIC_APP_URL=http://localhost:3001
NEXT_PUBLIC_API_URL=http://localhost:5000/api/v1
NEXT_PUBLIC_API_BASE_URL=http://localhost:5000
NEXT_PUBLIC_ADMIN_API_URL=/api/admin
AUTH_BACKEND_URL=http://localhost:5000
```

Do not commit `.env.local`.

## Local Docker Full-Stack Demo Mode

If you want a Docker-only local smoke test, run:

```powershell
docker compose -f backend/docker-compose.yml up --build
```

This starts PostgreSQL, Redis, API, Worker, and the production-built web container.

Use this for Docker parity checks, not for fast frontend iteration.

## Stop and Reset Local Stack

Stop containers:

```powershell
docker compose -f backend/docker-compose.yml down
```

Stop and delete local database/uploads/cache volumes:

```powershell
docker compose -f backend/docker-compose.yml down -v
```

Use `down -v` only when you intentionally want a clean local database.

## Quick Debug Guide

If a vehicle appears in admin but not on the public site:

1. Check public API:

   ```powershell
   Invoke-RestMethod http://localhost:5000/api/v1/vehicles
   ```

2. Confirm the vehicle status is `Available`.
3. Confirm the vehicle group and office are active.
4. Check the frontend API base URL:
   - local dev should use `http://localhost:5000/api/v1`
   - production should use `https://your-domain.com/api/v1`
5. If production env changed, redeploy because `NEXT_PUBLIC_*` values are build-time values.
