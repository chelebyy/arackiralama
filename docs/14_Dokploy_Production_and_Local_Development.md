# Dokploy Production and Local Development Runbook

Date: 2026-06-09

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
- `NOTIFICATIONS_PUBLIC_FRONTEND_BASE_URL` is server-side and must be the public HTTPS site origin used in account-claim links. Keep it equal to the production `NEXT_PUBLIC_APP_URL`, but configure it separately so local HTTP browser targets cannot be injected into the Production API.
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
4. Set env values using the production settings above, including `NOTIFICATIONS_PUBLIC_FRONTEND_BASE_URL=https://your-domain.com`, `PAYMENT_PROVIDER=Disabled`, and `PAYMENT_ENABLE_PAYMENTS=false`.
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

- Public customer membership/account claim is not part of the current product or release scope. No production email provider is selected or configured, and real account-claim delivery is not a current acceptance gate.
- Future automatic email is intended for reservation lifecycle notifications. The provider and exact trigger/event matrix have not been selected, so this runbook does not claim that reservation emails are currently delivered.
- `NOTIFICATIONS_PUBLIC_FRONTEND_BASE_URL` remains the required trusted HTTPS origin for the retained account-claim code if that capability is reintroduced. Setting the origin alone does not configure an email provider or prove delivery.
- This decision changes documentation only. It does not add credentials, change Dokploy runtime configuration, enable an email path, or remove the separate requirement to revalidate reachable original attack paths after deployment.

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
