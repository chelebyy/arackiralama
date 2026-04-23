# Handoff: Fix Console Errors, 404s, and i18n Rich Text Across All Locales

## Session Metadata
- Created: 2026-04-23 18:45:00
- Project: C:\All_Project\Araç Kiralama
- Branch: ui-v3
- Session duration: ~1.5 hours

### Recent Commits (for context)
  - c00aaa2 fix(frontend): language switcher onBlur bug, missing i18n translations, de.json namespaces, Arabic RTL
  - 7ab5f45 fix(frontend): add static asset guard and fix matcher regex in middleware
  - 8a7a5f3 fix(frontend): vehicle card price layout, migrate middleware to proxy, fix lint warnings
  - d0db5c2 fix(frontend): resolve globals.css stylelint and compatibility warnings

## Handoff Chain

- **Continues from**: [2026-04-23-130800-fix-language-switcher-i18n.md](./2026-04-23-130800-fix-language-switcher-i18n.md)
  - Previous title: Fix Language Switcher & i18n Translation Gaps
- **Supersedes**: None

> Review the previous handoff for full context before filling this one.

## Current State Summary

Four distinct runtime issues were identified and fixed across all 5 locales (`tr`, `en`, `ru`, `de`, `ar`). All pages now load with zero console errors and zero 404 responses:

1. **React "Functions are not valid as a React child" on `/contact`**: `ContactForm.tsx` used `t.rich()` with function-style rich text values (`terms: getTermsLink`), but the translation messages used `{terms}` placeholder syntax instead of XML-like `<terms>text</terms>` syntax that `t.rich()` requires. Fixed by converting all 5 locale files to XML tag syntax and reverting `ContactForm.tsx` to the function-chunk pattern.

2. **`/booking` 404**: The route `/booking` had no `page.tsx` — only nested `step1/` through `step4/` pages existed. Users hitting `/en/booking` got a 404. Fixed by adding `booking/page.tsx` that server-side redirects to `booking/step1`.

3. **`/track-reservation` 404**: `routing.ts` defines the pathname as `/track-reservation`, but the filesystem directory was named `track/`. Fixed by renaming `app/(public)/[locale]/track` to `track-reservation`.

4. **Missing vehicle images (6 x 404 on `/vehicles`)**: `public/images/vehicles/` did not exist at all. Vehicle cards and detail pages referenced `.png` files that were absent. Fixed by generating 30 placeholder SVG images (6 main vehicle + 18 detail gallery + 6 category images).

## Codebase Understanding

### Architecture Overview

- **Frontend**: Next.js 16 + React 19 + TypeScript + next-intl for i18n
- **Public pages**: Under `app/(public)/[locale]/` with locale prefix `always`
- **i18n messages**: Stored in `frontend/i18n/messages/{locale}.json`
- **Routing**: Custom routing config in `frontend/i18n/routing.ts` with localized pathnames
- **Static assets**: `public/` directory; images referenced as `/images/vehicles/...`

### Critical Files

| File | Purpose | Relevance |
|------|---------|-----------|
| `frontend/components/public/ContactForm.tsx` | Contact form with rich text links | Fixed `t.rich()` usage pattern |
| `frontend/i18n/messages/*.json` | All 5 locale files | Converted `termsAgreement` from `{terms}` to `<terms>text</terms>` |
| `frontend/app/(public)/[locale]/booking/page.tsx` | Booking entry point | New — redirects to `step1` |
| `frontend/app/(public)/[locale]/track-reservation/page.tsx` | Track reservation page | Moved from `track/` directory |
| `frontend/public/images/vehicles/` | Vehicle placeholder images | New — 30 SVG placeholders |
| `frontend/i18n/routing.ts` | next-intl routing config | Pathname mappings for all locales |

### Key Patterns Discovered

- **`t.rich()` requires XML tag syntax in messages**: If message is `"By submitting <terms>Terms of Service</terms>"`, then `t.rich()` tag prop is `terms: (chunks) => <Link>{chunks}</Link>`. Using `{terms}` syntax with function props causes React to render a function as a child.
- **`createNavigation` pathnames must match filesystem**: `routing.ts` pathname `/track-reservation` must correspond to `app/(public)/[locale]/track-reservation/`, not `track/`.
- **Parent routes without `page.tsx` return 404**: In App Router, `/booking` will 404 unless `booking/page.tsx` exists, even if children (`booking/step1/page.tsx`) exist.
- **Static images must exist in `public/`**: Next.js does not auto-generate missing images; 404s appear in browser console even if the page renders.

## Work Completed

### Tasks Finished

- [x] Fixed `ContactForm.tsx` `t.rich("termsAgreement")` misuse causing React function-as-child error
- [x] Updated `termsAgreement` message in `en.json` to XML tag syntax (`<terms>...<privacy>...`)
- [x] Updated `termsAgreement` message in `tr.json` to XML tag syntax
- [x] Updated `termsAgreement` message in `ru.json` to XML tag syntax
- [x] Updated `termsAgreement` message in `de.json` to XML tag syntax
- [x] Updated `termsAgreement` message in `ar.json` to XML tag syntax
- [x] Created `frontend/app/(public)/[locale]/booking/page.tsx` with redirect to `step1`
- [x] Renamed `app/(public)/[locale]/track` to `track-reservation` to match routing config
- [x] Created `frontend/public/images/vehicles/` directory with 30 placeholder SVG images
- [x] Verified build passes (144 pages generated)
- [x] Verified all 5 locales load all public pages without console errors
- [x] Verified `/booking` redirects to `/booking/step1` in all locales
- [x] Verified `/track-reservation` loads correctly in all locales
- [x] Verified `/vehicles` loads without image 404s in all locales

### Files Modified

| File | Changes | Rationale |
|------|---------|-----------|
| `frontend/components/public/ContactForm.tsx` | Restored `getTermsLink`/`getPrivacyLink` chunk functions; reverted mistaken direct JSX injection | `t.rich()` requires chunk functions, not raw JSX elements |
| `frontend/i18n/messages/en.json` | Changed `termsAgreement` from `{terms}` to `<terms>Terms of Service</terms>` | next-intl `t.rich()` parses XML-like tags, not curly braces |
| `frontend/i18n/messages/tr.json` | Same `termsAgreement` XML tag conversion | Consistent across all locales |
| `frontend/i18n/messages/ru.json` | Same `termsAgreement` XML tag conversion | Consistent across all locales |
| `frontend/i18n/messages/de.json` | Same `termsAgreement` XML tag conversion | Consistent across all locales |
| `frontend/i18n/messages/ar.json` | Same `termsAgreement` XML tag conversion | Consistent across all locales |
| `frontend/app/(public)/[locale]/booking/page.tsx` | New file: async server component redirecting to `booking/step1` | Parent route must exist or App Router returns 404 |
| `frontend/app/(public)/[locale]/track-reservation/page.tsx` | Moved from `track/page.tsx` | Filesystem must match `routing.ts` pathname mapping |
| `frontend/public/images/vehicles/*.svg` | 30 new placeholder SVGs for vehicle and category images | Eliminates 404 console errors on `/vehicles` and `/vehicles/[id]` |

### Decisions Made

| Decision | Options Considered | Rationale |
|----------|-------------------|-----------|
| Use XML tags in `t.rich()` messages | Could switch to plain string concatenation without rich text | XML tags are the idiomatic next-intl pattern; preserves link styling and translatability |
| Server redirect for `/booking` vs client redirect | Could use `useEffect` redirect in a client component | Server redirect is faster, SEO-friendly, and avoids hydration flash |
| Rename directory vs update routing config | Could change `routing.ts` to map `/track-reservation` to `track/` | Renaming directory is simpler; routing config already defines the canonical URL |
| SVG placeholders vs removing image references | Could remove `image` fields from mock data | Placeholders let UI layout render correctly; replacing with real photos is a future task |

## Pending Work

### Immediate Next Steps

1. **Replace placeholder vehicle images** — Add real `.png`/`.jpg` vehicle photos to `public/images/vehicles/` and update mock data references if filenames differ.
2. **Audit all `t.rich()` usages** — Search codebase for `t.rich(` and verify every message uses XML tag syntax, not curly brace placeholders.
3. **Add automated i18n key sync test** — A script that loads all `messages/*.json` files and verifies key parity across locales would prevent future translation gaps.

### Blockers/Open Questions

- None — all issues resolved and verified.

### Deferred Items

- Real vehicle photography upload
- Translation management system (Crowdin/Weblate)
- Automated i18n key parity testing

## Context for Resuming Agent

### Important Context

- **`t.rich()` message format is XML tags, not curly braces** — If you see `Functions are not valid as a React child`, check that the translation message uses `<tag>text</tag>` and the `t.rich()` call passes `(chunks) => <Component>{chunks}</Component>`.
- **App Router parent routes need `page.tsx`** — `/foo` will 404 even if `/foo/bar` exists, unless `/foo/page.tsx` (or `layout.tsx` that renders children) is present.
- **Routing pathname must match filesystem** — `routing.ts` defines the URL the user sees; the filesystem under `app/(public)/[locale]/` must match. Mismatches cause 404.
- **Placeholder images are SVGs** — They display text labels (e.g., "Fiat Egea") and are safe to replace 1-for-1 with real images using the same filenames.

### Assumptions Made

- The `tr.json` translation structure is the canonical source of truth.
- Placeholder SVGs are acceptable until real vehicle photos are provided.
- All 5 locales (`tr`, `en`, `ru`, `ar`, `de`) are the complete set.

### Potential Gotchas

- **Hot reload doesn't always pick up JSON changes** — restart dev server if translations seem stale.
- **Replacing SVG placeholders with PNGs** — If real images are `.png`, update the `image` and `images` arrays in `vehicles/page.tsx` and `vehicles/[id]/page.tsx`.
- **New routes need pathname mappings in `routing.ts`** — Always add entries for all 5 locales.

## Environment State

### Tools/Services Used

- Next.js 16.2.1 with Turbopack
- next-intl for i18n
- Playwright for automated browser verification
- pnpm for package management

### Active Processes

- Dev server running at `http://localhost:3000/` (Next.js frontend)

### Environment Variables

- Standard Next.js env vars
- No custom env vars required for this feature

## Related Resources

- [next-intl documentation — Rich text](https://next-intl.dev/docs/usage/messages#rich-text)
- `frontend/i18n/routing.ts` — routing and pathname config
- `frontend/components/public/ContactForm.tsx` — contact form with rich text
- `frontend/app/(public)/[locale]/booking/page.tsx` — booking redirect
- `frontend/app/(public)/[locale]/track-reservation/page.tsx` — track reservation

---

**Security Reminder**: Before finalizing, run `validate_handoff.py` to check for accidental secret exposure.
