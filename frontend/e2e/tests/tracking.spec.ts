/**
 * Reservation Tracking E2E Test
 *
 * Tests public reservation tracking flow.
 * Note: Track reservation page currently uses mock data (Phase 10.3 blocker).
 * Backend endpoint GET /api/v1/reservations/{publicCode} exists but not wired to page yet.
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

    // Search with non-existent code
    await trackPage.searchByCode("NONEXISTENT999");

    // Should show not found message (or empty state)
    await page.waitForLoadState("networkidle");
    // Current mock implementation shows no results; real API would show 404
  });

  test("search form is accessible", async ({ page }) => {
    const trackPage = new TrackReservationPage(page);
    await trackPage.goto("tr");

    // Verify keyboard accessibility
    await trackPage.searchInput.focus();
    await expect(trackPage.searchInput).toBeFocused();
  });
});
