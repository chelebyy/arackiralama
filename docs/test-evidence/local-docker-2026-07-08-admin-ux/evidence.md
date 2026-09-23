# Local Docker Browser Test Evidence

Date: 2026-07-08
Tester: Codex
Branch / Commit: `codex/localized-public-settings` / `c5ed27e` plus local working-tree changes
Docker Compose File: `backend/docker-compose.yml`
Frontend URL: `http://localhost:3001`
API URL: `http://localhost:5000`
Browser(s): Playwright Chromium headless

## Stack Status

- `docker compose -f backend\docker-compose.yml up -d --build` completed after the admin UX changes.
- `docker compose -f backend\docker-compose.yml ps` showed `rentacar-postgres`, `rentacar-redis`, `rentacar-api`, `rentacar-worker`, and `rentacar-web` running; PostgreSQL and Redis were healthy.
- `curl.exe -i http://localhost:5000/health` returned `HTTP/1.1 200 OK` with body `Healthy`.
- `curl.exe -I http://localhost:3001` returned `HTTP/1.1 307 Temporary Redirect` with `location: /tr`.

## Admin UX Refresh Pages

- Passed:
  - `/dashboard/settings/public-content`, pages tab
  - `/dashboard/settings/public-content`, contact tab
  - `/dashboard/settings/system`
- Viewports:
  - Desktop `1440x900`
  - Tablet `768x1024`
  - Mobile `375x812`
- Screenshots:
  - `desktop-admin-public-content-pages.png`
  - `desktop-admin-public-content-contact.png`
  - `desktop-admin-system-settings.png`
  - `tablet-admin-public-content-pages.png`
  - `tablet-admin-public-content-contact.png`
  - `tablet-admin-system-settings.png`
  - `mobile-admin-public-content-pages.png`
  - `mobile-admin-public-content-contact.png`
  - `mobile-admin-system-settings.png`

## Public Sanity Pages

- Passed:
  - `/tr/iletisim`
  - `/tr/privacy`
  - `/tr/terms`
- Viewports:
  - Desktop `1440x900`
  - Tablet `768x1024`
  - Mobile `375x812`
- Screenshots:
  - `desktop-public-contact.png`
  - `desktop-public-privacy.png`
  - `desktop-public-terms.png`
  - `tablet-public-contact.png`
  - `tablet-public-privacy.png`
  - `tablet-public-terms.png`
  - `mobile-public-contact.png`
  - `mobile-public-privacy.png`
  - `mobile-public-terms.png`

## Network / Console

- Unexpected application `4xx`: none.
- Unexpected application `5xx`: none.
- Material console errors: none.
- Known local console noise: `Google Analytics key not provided.`
- Raw browser summary: `browser-summary.json`.

## Blockers

- [x] None for this browser validation pass.

## Decision

- [x] Admin Public Site & Contact UX validation gate passed for the targeted pages.
