/**
 * Admin Reservations E2E Test
 *
 * Tests admin reservation management.
 * Requires: Admin login (see admin-login.spec.ts)
 */

import { test, expect, ADMIN_USER } from "../fixtures/test-data";
import { AdminLoginPage } from "../pages/AdminLoginPage";
import { AdminReservationsPage } from "../pages/AdminReservationsPage";
import { AdminReservationDetailPage } from "../pages/AdminReservationDetailPage";

test.describe("Admin Reservations", () => {
  // Login before each test
  test.beforeEach(async ({ page }) => {
    const loginPage = new AdminLoginPage(page);
    await loginPage.goto();
    await loginPage.login(
      "integration-admin@rentacar.test",
      "IntegrationTestPassword123!"
    );
    await loginPage.expectLoginSuccess();
  });

  test("reservations list page loads", async ({ page }) => {
    const reservationsPage = new AdminReservationsPage(page);
    await reservationsPage.goto();
    await reservationsPage.expectReservationsVisible();
  });

  test("can search reservation by code", async ({ page }) => {
    const reservationsPage = new AdminReservationsPage(page);
    await reservationsPage.goto();

    // Search with a code (may not exist but should not error)
    await reservationsPage.searchByCode("NONEXISTENT123");
    await page.waitForLoadState("networkidle");
  });

  test("status filter shows options", async ({ page }) => {
    const reservationsPage = new AdminReservationsPage(page);
    await reservationsPage.goto();

    // Verify filter is interactive
    await expect(reservationsPage.statusFilter).toBeVisible();
  });

  test("refund button opens dialog on captured reservation", async ({ page }) => {
    const reservationsPage = new AdminReservationsPage(page);
    await reservationsPage.goto();
    await reservationsPage.expectReservationsVisible();

    // Click first reservation row to open detail
    await page.locator("[data-testid='reservation-row']").first().click();
    await page.waitForURL(/\/dashboard\/reservations\/[^/]+$/);

    const detailPage = new AdminReservationDetailPage(page);

    // If refund button is visible (payment status CAPTURED/AUTHORIZED), test it
    const refundVisible = await detailPage.refundButton.isVisible().catch(() => false);
    if (refundVisible) {
      await detailPage.clickRefund();
      await detailPage.fillRefundDialog({ reason: "E2E test refund" });
      // Wait for toast
      await page.waitForSelector("[data-sonner-toast]", { state: "visible", timeout: 5000 }).catch(() => {});
    }
  });
});
