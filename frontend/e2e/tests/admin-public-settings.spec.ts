/**
 * Admin public site settings E2E smoke.
 *
 * Requires the Docker stack with seeded integration admin credentials.
 */

import { test, expect, ADMIN_USER } from "../fixtures/test-data";
import { AdminLoginPage } from "../pages/AdminLoginPage";

test.describe("Admin Public Site Settings", () => {
  test("renders localized public setting controls", async ({ page }) => {
    const loginPage = new AdminLoginPage(page);
    await loginPage.goto();
    await loginPage.login(ADMIN_USER.email, ADMIN_USER.password);
    await loginPage.expectLoginSuccess();

    await page.goto("/dashboard/settings/public-content");

    await expect(page.getByRole("heading", { name: "İçerik Yönetimi" })).toBeVisible();
    await expect(page.getByRole("tab", { name: "Sayfalar" })).toBeVisible();
    await expect(page.getByRole("tab", { name: "İletişim", exact: true })).toBeVisible();
  });
});
