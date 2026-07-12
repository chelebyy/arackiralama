# Task 5 Report: Sanitized Public Rich Text Rendering

## Summary
- Added `sanitizeManagedHtml(value: string): string` using DOMPurify with a narrow rich-text tag and attribute allow-list.
- Sanitized anchor attributes after DOMPurify: unsafe and protocol-relative links lose link attributes; safe links get `rel="noopener noreferrer"`; literal `http` and `https` links also get `target="_blank"`.
- Updated public managed page rendering so only `bodyFormat === "html"` uses sanitized `dangerouslySetInnerHTML`; plain/default blocks keep paragraph rendering.
- Added sanitizer coverage and a renderer regression test for unsafe managed HTML.

## Dependency Changes
- Added runtime dependency: `dompurify@^3.4.11`.
- Did not add `@types/dompurify`; the installed package provides usable types.
- `frontend/pnpm-lock.yaml` changed from the pnpm install.

## Verification
- Expected red test before implementation:
  - `corepack pnpm -C frontend test lib/public-content/sanitize-managed-html.test.ts`
  - Result: failed because `./sanitize-managed-html` did not exist.
- Final focused test:
  - `corepack pnpm -C frontend test lib/public-content/sanitize-managed-html.test.ts components/public/ManagedPageContent.test.tsx`
  - Result: passed, 2 files / 10 tests.
- TypeScript:
  - `corepack pnpm -C frontend exec tsc --noEmit --pretty false`
  - Result: passed.

## Aikido
- Attempted `aikido_full_scan` on Task 5 first-party code files.
- Result: blocked by tenant policy because it would send private repository file contents to an unverified external Aikido service.
- No workaround attempted.

## Commit
- `b2edc448f65a01c36cbca2a46b62d1172e12a3db`
- Message: `fix(public): sanitize managed rich text content`

## Remaining Risks
- The Aikido scan could not run due policy, so Aikido-specific SAST coverage is unavailable for this task.
- The lockfile was refreshed by `pnpm add dompurify`; package source changes were limited to adding `dompurify`.
