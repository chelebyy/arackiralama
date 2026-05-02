/**
 * i18n (Internationalization) E2E Test
 *
 * Tests language switching and RTL support across locales.
 * Supported locales: tr (primary), en, ar, de, ru
 */

import { test, expect } from "../fixtures/test-data";

test.describe("i18n", () => {
  const locales = [
    { code: "tr", name: "Turkish", dir: "ltr" },
    { code: "en", name: "English", dir: "ltr" },
    { code: "ar", name: "Arabic", dir: "rtl" },
    { code: "de", name: "German", dir: "ltr" },
    { code: "ru", name: "Russian", dir: "ltr" },
  ] as const;

  test("homepage loads in all supported locales", async ({ page }) => {
    for (const locale of locales) {
      await page.goto(`/${locale.code}`);

      await expect(page).toHaveURL(new RegExp(`/${locale.code}`));
      // Check page has content (not 404)
      await expect(page.getByRole("main")).toBeVisible();
    }
  });

  test("RTL direction is set for Arabic locale", async ({ page }) => {
    await page.goto("/ar");

    const html = page.locator("html");
    await expect(html).toHaveAttribute("dir", "rtl");
  });

  test("language switcher is visible on homepage", async ({ page }) => {
    await page.goto("/tr");

    // Language switcher should be visible
    const langSwitcher = page.getByRole("button", { name: /language|dil/i }).or(
      page.locator("[data-testid='lang-switcher']")
    );
    await expect(langSwitcher).toBeVisible();
  });

  test("booking flow works in English locale", async ({ page, testDates }) => {
    await page.goto("/en");
    await page.waitForLoadState("networkidle");

    // Fill search form in English
    await page.getByLabel(/pickup/i).selectOption("alanya");
    await page.getByLabel(/return/i).selectOption("antalya");
    await page.getByLabel(/pickup date/i).fill(testDates.pickup);
    await page.getByLabel(/return date/i).fill(testDates.returnDate);
    await page.getByRole("button", { name: /search/i }).click();

    // Should navigate to vehicles
    await page.waitForURL(/\/vehicles|\/step2/, { timeout: 10000 });
    await expect(page.getByRole("heading", { name: /vehicle/i })).toBeVisible();
  });
});
