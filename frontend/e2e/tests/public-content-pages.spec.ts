import { expect, test } from "@playwright/test";

const publicRoutes = ["/tr", "/tr/iletisim", "/tr/privacy", "/tr/terms"];

test.describe("Public content pages", () => {
  for (const route of publicRoutes) {
    test(`${route} renders without an error page`, async ({ page }) => {
      const response = await page.goto(route, { waitUntil: "domcontentloaded" });

      expect(response, `${route} should return a response`).not.toBeNull();
      expect(response?.ok(), `${route} should return a successful status`).toBe(true);
      await expect(page.locator("body")).not.toContainText(
        /Application error|Internal Server Error|Unhandled Runtime Error/i
      );
      await expect(page.locator("h1").first()).toBeVisible();
    });
  }
});
