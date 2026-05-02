/**
 * Admin Reservations E2E Test
 *
 * Tests admin reservation management.
 * Requires: Admin login (see admin-login.spec.ts)
 */

import { test, expect, ADMIN_USER } from "../fixtures/test-data";
import { AdminLoginPage } from "../pages/AdminLoginPage";
import { AdminReservationsPage } from "../pages/AdminReservationsPage";

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
});
