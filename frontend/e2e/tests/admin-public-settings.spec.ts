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

    await page.goto("/dashboard/settings/system");

    await expect(page.getByText("Public Site Ayarları")).toBeVisible();
    await expect(page.getByText("Sayfalar")).toBeVisible();
    await expect(page.getByText(/5 dil/).first()).toBeVisible();
    await expect(page.getByText("Dil bazlı içerik").first()).toBeVisible();

    await page.getByRole("tab", { name: "EN" }).first().click();

    await expect(page.getByPlaceholder("Bu dilde bağlantı başlığı").first()).toBeVisible();
    await expect(page.getByPlaceholder("Bu dilde kanal başlığı").first()).toBeVisible();
  });
});
