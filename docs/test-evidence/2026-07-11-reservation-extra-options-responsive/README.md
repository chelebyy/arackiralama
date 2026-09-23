# Reservation Extra Options Responsive Browser Evidence

**Date:** 2026-07-11
**Target:** Docker production web at `http://localhost:3001`
**Branch:** `codex/reservation-extra-options`
**Starting commit:** `63b0fb440cd01a328ffba40cfa5577b1f4c64597`

## Result

Focused Playwright Chromium passed 1/1 for the reservation-extra Step 3 and Step 4 layouts at desktop (1440x1000), tablet (834x1112), and mobile (390x844).

- Six full-page screenshots were captured and visually inspected.
- Every viewport passed the horizontal-overflow assertion.
- Visible icon-bearing buttons had accessible names.
- All captured application API responses were below HTTP 400.
- Catalog and quote responses carried `Cache-Control: no-store`.
- No unexpected console errors were recorded. The local production bundle emitted the already-known environment message `Google Analytics key not provided.` once per navigation; it is recorded separately and is not a reservation-extra failure.
- The network ledger contains only method, path, status, content type, and cache-control metadata. It contains no query values, request/response bodies, cookies, tokens, card data, or customer data.

## Command

```powershell
$env:E2E_BASE_URL='http://localhost:3001'
playwright test e2e/tests/reservation-extra-options.spec.ts --grep "layouts stay usable" --project=chromium --workers=1
```

## Files

- `step3-desktop.png`, `step3-tablet.png`, `step3-mobile.png`
- `step4-desktop.png`, `step4-tablet.png`, `step4-mobile.png`
- `browser-evidence.json`

This closes the final local workflow row in checklist section 6.6. CI, Aikido, deployment/rollback rehearsal, and the 14-day production legacy-adapter observation window remain open and are not implied by this result.
