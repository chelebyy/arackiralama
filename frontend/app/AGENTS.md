# Frontend App Router Knowledge Base

**Scope:** Next.js 16 App Router — route groups, i18n, layouts, middleware
**Parent:** `frontend/`

## Structure
```
app/
├── page.tsx                    # Hardcoded redirect("/tr") — locale entry
├── layout.tsx                  # Root layout: theme, locale, providers
├── api/                        # Next.js Route Handlers (internal proxy)
├── (admin)/                    # Admin dashboard route group
│   └── dashboard/
│       ├── (auth)/             # Protected admin pages (sidebar + header)
│       │   ├── layout.tsx
│       │   ├── default/page.tsx
│       │   ├── vehicles/
│       │   ├── reservations/
│       │   ├── users/
│       │   └── ...
│       └── (guest)/            # Login page (no auth required)
│           └── login/page.tsx
└── (public)/                   # Public-facing route group
    └── [locale]/               # i18n dynamic segment
        ├── layout.tsx
        ├── page.tsx            # Homepage
        ├── vehicles/
        ├── booking/
        │   ├── page.tsx
        │   ├── step1/
        │   ├── step2/
        │   ├── step3/
        │   ├── step4/
        │   └── confirmation/
        ├── about/
        ├── contact/
        ├── terms/
        ├── privacy/
        └── track-reservation/
```

## Where to Look
| Task | Location | Notes |
|------|----------|-------|
| Add public page | `(public)/[locale]/{page}/page.tsx` | Wrap with public layout |
| Add admin page | `(admin)/dashboard/(auth)/{page}/page.tsx` | Wrap with admin shell |
| Add API route | `api/{route}/route.ts` | Internal proxy handlers |
| Modify root layout | `layout.tsx` | ThemeProvider, ActiveThemeProvider, locale init |
| Modify public layout | `(public)/[locale]/layout.tsx` | Locale-specific wrappers |
| Modify admin layout | `(admin)/dashboard/(auth)/layout.tsx` | AppSidebar + SiteHeader shell |
| Update i18n | `../i18n/messages/{locale}.json` | ar, de, en, ru, tr |

## Non-Standard Patterns
- **Hardcoded locale redirect**: `app/page.tsx` unconditionally does `redirect("/tr")` instead of middleware-based locale detection. Locale is only read from cookies after first load.
- **Nested route groups**: `(admin)/dashboard/(auth)/` uses double parentheses for layout segmentation — unusual but valid in Next.js.
- **Dual layout system**: Public and admin share the same `app/` tree but have completely separate design languages (corporate-minimal vs shadcn/ui).

## Entry Points
- **`layout.tsx`** — Root layout. Handles: locale detection from cookies, theme settings (preset/scale/radius/contentLayout), ThemeProvider, ActiveThemeProvider, Toaster, NextTopLoader, GoogleAnalyticsInit.
- **`page.tsx`** (root) — Simple `redirect("/tr")` to force Turkish locale entry.
- **`(public)/[locale]/layout.tsx`** — Public layout with locale context.
- **`(admin)/dashboard/(auth)/layout.tsx`** — Admin shell with AppSidebar + SiteHeader.

## i18n Setup
- Uses `next-intl` with config at `../i18n/config.ts`
- Routing config in `../i18n/routing.ts` with `mode: "always"`
- Messages stored in `../i18n/messages/{locale}.json`
- Supported locales: ar, de, en, ru, tr

## Design Rules
- **Public pages**: Corporate-minimal, light-only, desktop-first. NO shadcn/ui components.
- **Admin pages**: CAN use shadcn/ui components and dashboard design language.
- Never mix the two design languages on the same page.

## Notes
- `next.config.ts` enables image domains: `localhost` (http) and `bundui-images.netlify.app` (https)
- `app/api/` contains internal API proxy routes (not external-facing)
- Route groups with parentheses do not affect URL structure
