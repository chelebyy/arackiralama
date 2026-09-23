/**
 * Reservation Tracking E2E Test
 *
 * Tests public reservation tracking flow.
 */

import { test, expect } from "../fixtures/test-data";
import { TrackReservationPage } from "../pages/TrackReservationPage";

test.describe("Reservation Tracking", () => {
  test("track page loads in Turkish", async ({ page }) => {
    const trackPage = new TrackReservationPage(page);
    await trackPage.goto("tr");

    await expect(trackPage.searchInput).toBeVisible();
    await expect(trackPage.searchButton).toBeVisible();
  });

  test("track page loads in English", async ({ page }) => {
    const trackPage = new TrackReservationPage(page);
    await trackPage.goto("en");

    await expect(trackPage.searchInput).toBeVisible();
  });

  test("search with invalid code shows not found", async ({ page }) => {
    const trackPage = new TrackReservationPage(page);
    await trackPage.goto("tr");

    await trackPage.searchByCode("NONEXISTENT999");
    await trackPage.expectNotFound();
  });

  test("search form is accessible", async ({ page }) => {
    const trackPage = new TrackReservationPage(page);
    await trackPage.goto("tr");

    // Verify keyboard accessibility
    await trackPage.searchInput.focus();
    await expect(trackPage.searchInput).toBeFocused();
  });
});
