/**
 * Smoke Test - Homepage Loads
 *
 * Verifies the application is running and accessible.
 */

import { test, expect } from "../fixtures/test-data";

test.describe("Smoke", () => {
  test("homepage loads in Turkish locale", async ({ page }) => {
    await page.goto("/tr");

    // Check page loaded
    await expect(page).toHaveURL(/\/tr/);

    // Check key elements visible
    await expect(page.getByRole("heading", { name: /kiralık|araç/i })).toBeVisible();
  });

  test("homepage loads in English locale", async ({ page }) => {
    await page.goto("/en");

    await expect(page).toHaveURL(/\/en/);
    await expect(page.getByRole("heading", { name: /rent.*car|vehicle/i })).toBeVisible();
  });

  test("admin login page loads", async ({ page }) => {
    await page.goto("/dashboard/login");

    await expect(page.getByLabel(/email/i)).toBeVisible();
    await expect(page.getByLabel(/password/i)).toBeVisible();
  });
});
