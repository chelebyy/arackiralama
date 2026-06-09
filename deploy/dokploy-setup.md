# Dokploy Production Deployment Guide

## 1. VPS prerequisites

1. Provision a Linux VPS with enough RAM/CPU for five containers (PostgreSQL, Redis, API, Worker, Web).
2. Install Docker Engine and Docker Compose plugin.
3. Install Dokploy on the VPS by following the official Dokploy installer.
4. Verify the Dokploy-managed Docker network exists (`dokploy-network` by default).

## 2. Prepare the repository

1. Commit these production deployment files:
   - `docker-compose.yml`
   - `frontend/Dockerfile`
   - `frontend/.dockerignore`
   - `.env.example`
   - `deploy/dokploy-setup.md`
2. In Dokploy, this repository should deploy from the project root because the production compose file lives at `/docker-compose.yml`.
3. Copy `.env.example` to a secure local file and replace every placeholder before entering values into Dokploy.

## 3. Create the application in Dokploy

1. Open the Dokploy dashboard.
2. Create a new **Compose** application.
3. Connect the Git repository.
4. Select the branch to deploy.
5. Set the compose file path to:

   ```
   docker-compose.yml
   ```

## 4. Configure environment variables

Add the variables from `.env.example` inside the Dokploy environment section.

### Minimum required variables

- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `DATABASE_URL`
- `REDIS_PASSWORD`
- `REDIS_CONNECTION_STRING`
- `JWT_ISSUER`
- `JWT_AUDIENCE`
- `JWT_SECRET`
- `NEXT_PUBLIC_APP_URL`
- `NEXT_PUBLIC_API_URL`
- `NEXT_PUBLIC_API_BASE_URL`
- `AUTH_BACKEND_URL`

### Recommended values for a single public domain

- `AUTH_BACKEND_URL=http://api:8080`
- `NEXT_PUBLIC_API_BASE_URL=https://app.your-domain.com`
- `NEXT_PUBLIC_API_URL=https://app.your-domain.com/api/v1`
- `NEXT_PUBLIC_ADMIN_API_URL=/api/admin`
- `NEXT_PUBLIC_APP_URL=https://app.your-domain.com`
- `DATABASE_AUTO_MIGRATE_ON_STARTUP=true` for first deployment, then optionally switch to controlled migrations later

The web app proxies `/api/v1/...` and `/uploads/...` to `AUTH_BACKEND_URL`, so browsers do not need direct access to the internal Docker or Tailscale API address.

## 5. Domain configuration

Dokploy already provides Traefik, so no nginx service is needed in Compose.

Typical single-domain setup:

- `web` → `app.your-domain.com`
- public API browser traffic → `https://app.your-domain.com/api/v1/...`
- uploaded media browser traffic → `https://app.your-domain.com/uploads/...`
- server-side proxy target → `AUTH_BACKEND_URL=http://api:8080`

In Dokploy:

1. Open the deployed app services.
2. Attach a domain to the `web` service.
3. Ensure the public environment variables match the web domain.

If you prefer a separate public API domain, attach that domain to the `api` service and set `NEXT_PUBLIC_API_URL` / `NEXT_PUBLIC_API_BASE_URL` to that API domain instead.

## 6. SSL/TLS

Dokploy manages Traefik and can issue SSL certificates automatically.

1. Point DNS records to the VPS first.
2. Enable HTTPS/automatic certificate provisioning for each mapped domain in Dokploy.
3. After certificates are issued, verify:
   - `https://app.your-domain.com`
   - `https://api.your-domain.com/health`

## 7. Health check configuration

This Compose file already defines health checks for every service:

- `postgres` → `pg_isready`
- `redis` → `redis-cli ping`
- `api` → `GET /health` on port `8080`
- `web` → HTTP request to port `3000`
- `worker` → process check against the running worker entrypoint

Recommended Dokploy monitoring target:

```text
https://api.your-domain.com/health
```

## 8. Deployment flow

1. Trigger the first deployment from Dokploy.
2. Wait until `postgres` and `redis` report healthy.
3. Confirm `api`, `worker`, and `web` become healthy afterwards.
4. Open service logs in Dokploy if any container stays in `starting` or `unhealthy`.

## 9. Backup notes

### PostgreSQL

- Persisted in the `postgres_data` Docker volume.
- Schedule regular database backups (`pg_dump` or volume-level backup snapshots).
- Keep backups off-server.

### Redis

- Persisted in the `redis_data` Docker volume.
- Redis should be treated as recoverable cache/job state storage unless you explicitly depend on persistence.

### Environment variables

- Export Dokploy environment settings securely.
- Store secrets in a password manager, not in Git.

## 10. Operational notes

- `frontend/Dockerfile` uses Next.js standalone output for smaller runtime images.
- `frontend/.dockerignore` excludes `node_modules`, `.next`, and local env files from the Docker build context.
- Existing backend Dockerfiles and `backend/docker-compose.yml` remain untouched.
