/**
 * Track Reservation Page Object
 *
 * Usage:
 *   const trackPage = new TrackReservationPage(page);
 *   await trackPage.goto('tr');
 *   await trackPage.searchByCode('ABC123');
 *   await trackPage.expectReservationFound();
 */

import { Page, Locator, expect } from "@playwright/test";

export class TrackReservationPage {
  readonly page: Page;
  readonly searchInput: Locator;
  readonly searchButton: Locator;
  readonly errorMessage: Locator;
  readonly reservationCard: Locator;

  constructor(page: Page) {
    this.page = page;
    this.searchInput = page.getByLabel(/rezervasyon|reservation|kod|code/i);
    this.searchButton = page.getByRole("button", { name: /takip|track|ara|search/i });
    this.errorMessage = page.getByRole("alert");
    this.reservationCard = page.locator("[data-testid='reservation-card']");
  }

  async goto(locale = "tr") {
    await this.page.goto(`/${locale}/track-reservation`);
  }

  async searchByCode(code: string) {
    await this.searchInput.fill(code);
    await this.searchButton.click();
    await this.page.waitForLoadState("networkidle");
  }

  async expectReservationFound() {
    await expect(this.reservationCard).toBeVisible();
  }

  async expectNotFound(message?: string) {
    if (message) {
      await expect(this.errorMessage).toContainText(message);
    } else {
      await expect(this.errorMessage).toBeVisible();
    }
  }

  async getStatus(): Promise<string> {
    const statusBadge = this.reservationCard.locator("[data-testid='status-badge']");
    return statusBadge.textContent() ?? "";
  }
}
