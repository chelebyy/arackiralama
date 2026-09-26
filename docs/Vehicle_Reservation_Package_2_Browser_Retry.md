# Package 2 browser retry — September 26, 2026

Current status: scoped local acceptance is complete; see the [September 26 acceptance continuation](Vehicle_Reservation_Package_2_Acceptance.md). The initial retry observations below are historical and do not override that closure.

## Initial retry outcome

The saved-browser-permission blocker is no longer present after the user restarted Codex. Supported cua_repl opened and operated the local production-build frontend. No alternate browser-control mechanism or permission bypass was used. This establishes recovery of the observed blocker, not its underlying cause.

## Verified local behavior

- The unconfigured office editor displayed that automatic booking is unavailable and did not invent notice/preparation values.
- Both synthetic offices accepted explicitly entered test settings: zero notice, 60 minutes preparation, Saturday pickup 09:00–18:00 and Tuesday return 09:00–18:00. Gazipasa settings were reopened and retained their numeric weekdays and times. These are disposable fixture choices, not live business rules.
- Public home and dated catalogue loaded. The October 10–13 itinerary displayed TRY 3600 for each available vehicle.
- Fiat Catalog 1 retained its exact vehicle ID through detail, customer information, quote and confirmation. The real local booking displayed “Rezervasyonunuz kesinleşti” and payment due at pickup.
- Database verification: public code `C95-E543-WW2`, status `Confirmed`, vehicle `a6b3c7f1-e992-4e19-b62e-8a185a0b19ce`, total 3600, occupied-until `2026-10-13T08:00:00Z` (one hour after return), zero payment intents.
- On the final build, opening step 4 again for that allocated exact vehicle produced “Selected vehicle is unavailable for this itinerary.” No sibling was substituted. This does not prove a mid-submission race or response-loss recovery. The API error remained English in the Turkish screen; localized error presentation remains a browser acceptance gap.
- Synthetic records used the isolated `rentacar_pkg2_browser_20260926` database. No production state was changed.

## Corrections discovered by the browser

1. Office policy editor sent string weekdays while the API expected numeric `DayOfWeek`. `OperatingWindow.day` and the editor now use 0–6. A regression test covers numeric API data and selection changes; actual save/reopen passed.
2. The public Next proxy omitted `X-Session-Id` and `Idempotency-Key`. A narrow allowlist now forwards these booking headers. Tests verify quote/reservation forwarding while withholding cookies, authorization and untrusted forwarded-IP headers. A real quote and reservation passed through the proxy.
3. On a full step-4 reload, an automatic quote could precede restored office IDs and send URL office codes as GUIDs. Automatic quoting now waits for restored booking dates. A delayed-restoration regression test passes. Customer/driver personal data is not retained across a full reload; the UI asks the user to return to the previous step.

Focused verification after these changes: **7 files / 42 tests passed**. Earlier full-suite receipts remain recorded separately. Final production build/TypeScript passed. Full lint passed with zero errors and the one pre-existing SearchForm test warning; diff whitespace and all eight updated documents' local links passed. No new broad security scan was run. The proxy review is limited to the explicitly forwarded headers and unchanged fixed upstream destination.

## Local test setup and evidence

- API: `http://127.0.0.1:5000`; frontend: `http://127.0.0.1:3108`.
- Build-time `NEXT_PUBLIC_API_URL=http://127.0.0.1:3108/api/v1`, `NEXT_PUBLIC_ADMIN_API_URL=/api/admin`; server `AUTH_BACKEND_URL=http://127.0.0.1:5000`.
- A relative public API base broke catalogue image URL normalization; the test build was corrected to use an absolute base. Public environment variables must be set when building.
- Browser native time fields required keyboard input to update React state; merely displaying a filled DOM value did not establish that the application received it. Network observations distinguished this tool interaction from the API weekday contract defect.
- Screenshots: `C:/Users/muham/.codex/visualizations/2026/09/26/01a0dd66-b249-7f90-9812-df3d6c77ef78/package2-office-policy.png` and `package2-booking-confirmed.png` in the same directory.

## Follow-up closure

The subsequently authorized browser continuation completed group-less authoring, policy boundaries, concurrent conflict/no-substitution recovery, changed-offer acceptance, post-commit response-loss replay, five locales/Arabic RTL and responsive/keyboard checks. It also corrected localized errors, language-query retention, actual licence dates, in-memory Back recovery and summary-verification retry. Detailed evidence and physical-device/accessibility limitations are in the acceptance record. Production activation still requires real operator settings and separate release authorization. No commit, push, PR, merge, deployment or production migration was performed.
