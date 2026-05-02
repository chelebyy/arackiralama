/**
 * Mobile Responsiveness E2E Test
 *
 * Tests that public pages render correctly on mobile viewports.
 * Mobile-first design principle: layouts should adapt gracefully.
 */

import { test, expect } from "../fixtures/test-data";

test.describe("Mobile Responsiveness", () => {
  const mobileViewports = [
    { name: "iPhone 12", width: 390, height: 844 },
    { name: "iPad", width: 768, height: 1024 },
    { name: "Android", width: 360, height: 800 },
  ];

  for (const viewport of mobileViewports) {
    test(`${viewport.name} (${viewport.width}x${viewport.height}) - homepage is usable`, async ({
      page,
    }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await page.goto("/tr");
      await page.waitForLoadState("networkidle");

      // Page should not have horizontal overflow
      const body = page.locator("body");
      const bodyWidth = await body.evaluate((el) => el.scrollWidth);
      expect(bodyWidth).toBeLessThanOrEqual(viewport.width);

      // Key elements should be visible
      await expect(page.getByRole("main")).toBeVisible();
    });
  }

  test("search form is usable on mobile", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/tr");
    await page.waitForLoadState("networkidle");

    // Search form elements should be accessible
    const pickupSelect = page.getByLabel(/pickup|alış/i);
    await expect(pickupSelect).toBeVisible();

    const searchButton = page.getByRole("button", { name: /ara|search/i });
    await expect(searchButton).toBeVisible();
  });

  test("admin login is accessible on mobile", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/dashboard/login");

    await expect(page.getByLabel(/email/i)).toBeVisible();
    await expect(page.getByLabel(/password/i)).toBeVisible();
    await expect(page.getByRole("button", { name: /giriş|login/i })).toBeVisible();
  });
});
