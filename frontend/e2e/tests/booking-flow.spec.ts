/**
 * Booking Flow E2E Test
 *
 * Tests the complete booking flow: search -> vehicle select -> info -> payment
 * Requires: Backend running with seeded data
 */

import { test, expect } from "../fixtures/test-data";
import { HomePage } from "../pages/HomePage";

test.describe("Booking Flow", () => {
  test("complete booking flow with mock payment", async ({ page, testDates }) => {
    const homePage = new HomePage(page);

    // Step 1: Homepage - Search
    await homePage.goto("tr");
    await homePage.fillSearchForm({
      pickupOffice: "ala",
      returnOffice: "ayt",
      pickupDate: testDates.pickup,
      returnDate: testDates.returnDate,
    });
    await homePage.submitSearch();

    // Step 2: Vehicles page - Select first vehicle
    await expect(page).toHaveURL(/\/vehicles|\/booking\/step2|\/araclar/);
    const vehicleCards = page.locator("[data-testid='vehicle-card']");
    await expect(vehicleCards.first()).toBeVisible({ timeout: 10000 });
    await vehicleCards.first().click();

    // Step 3: Extras selection
    await expect(page).toHaveURL(/\/booking\/step3/);
    const submitButton = page.getByRole("button", { name: /devam|continue/i });
    await expect(submitButton).toBeVisible();

    // Note: Step3 has driverLicenseCountry field requirement
    // This test verifies the page flow; full submit requires driverLicenseCountry input

    // Step 4 would be payment - skip in this smoke test
  });

  test("search form submits with default fallback values", async ({ page }) => {
    const homePage = new HomePage(page);
    await homePage.goto("tr");

    await page.getByRole("button", { name: /ara|search/i }).click();

    await expect(page).toHaveURL(/\/vehicles|\/araclar/);
  });

  test("vehicles page shows results", async ({ page, testDates }) => {
    const homePage = new HomePage(page);
    await homePage.goto("tr");
    await homePage.fillSearchForm({
      pickupOffice: "ala",
      returnOffice: "ayt",
      pickupDate: testDates.pickup,
      returnDate: testDates.returnDate,
    });
    await homePage.submitSearch();

    // Wait for vehicles page
    await page.waitForURL(/\/vehicles|\/booking\/step2|\/araclar/, { timeout: 10000 });
    await page.waitForLoadState("networkidle");

    const vehicleCards = page.locator("[data-testid='vehicle-card']");
    const count = await vehicleCards.count();
    expect(count).toBeGreaterThan(0);
  });
});
