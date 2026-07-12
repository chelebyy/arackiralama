# Task 9 Report: Separate Public Content from System Settings

## Summary
- Removed duplicate managed page and contact-page editing UI from the system settings screen.
- Kept company identity, navigation links, social links, and footer link editing in system settings.
- Preserved legacy `pages` and `contactPage*` payload fields as hidden/pass-through data so saving system settings does not wipe public content managed in the new workspace.
- Updated the admin public settings E2E smoke to target `/dashboard/settings/public-content`.

## Verification
- `corepack pnpm exec vitest run SystemSettingsPage.test.tsx PublicContentManager.test.tsx` from `frontend`: PASS, 2 files / 7 tests.
- `& .\node_modules\.bin\eslint.ps1 'app/(admin)/dashboard/(auth)/settings/system/page.tsx' 'app/(admin)/dashboard/(auth)/settings/system/SystemSettingsPage.test.tsx' 'e2e/tests/admin-public-settings.spec.ts'` from `frontend`: PASS.
- `git diff --check` for Task 9 files: PASS.
- Code-reviewer subagent: no Critical/Important findings; minor non-empty contact preservation gap fixed with richer test fixture.
- Aikido: blocked by external private-code policy when full private file contents were submitted to `aikido_full_scan`; no workaround attempted.

## Files
- `frontend/app/(admin)/dashboard/(auth)/settings/system/page.tsx`
- `frontend/app/(admin)/dashboard/(auth)/settings/system/SystemSettingsPage.test.tsx`
- `frontend/e2e/tests/admin-public-settings.spec.ts`

## Remaining Risks
- Browser E2E was not run locally in this slice; the Playwright smoke was updated but not executed.
- Aikido scan did not complete due policy block.
