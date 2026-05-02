/**
 * Admin Reservations Page Object
 *
 * Usage:
 *   const reservationsPage = new AdminReservationsPage(page);
 *   await reservationsPage.goto();
 *   await reservationsPage.filterByStatus('Held');
 */

import { Page, Locator, expect } from "@playwright/test";

export class AdminReservationsPage {
  readonly page: Page;
  readonly urlPattern = /\/dashboard\/reservations/;
  readonly statusFilter: Locator;
  readonly searchInput: Locator;
  readonly reservationRows: Locator;
  readonly firstRow: Locator;

  constructor(page: Page) {
    this.page = page;
    this.statusFilter = page.getByLabel(/durum|status/i);
    this.searchInput = page.getByPlaceholder(/ara|search/i);
    this.reservationRows = page.locator("table tbody tr");
    this.firstRow = page.locator("table tbody tr").first();
  }

  async goto() {
    await this.page.goto("/dashboard/reservations");
    await expect(this.page).toHaveURL(this.urlPattern);
  }

  async filterByStatus(status: string) {
    await this.statusFilter.selectOption({ label: status });
    await this.page.waitForLoadState("networkidle");
  }

  async searchByCode(code: string) {
    await this.searchInput.fill(code);
    await this.page.keyboard.press("Enter");
    await this.page.waitForLoadState("networkidle");
  }

  async clickFirstReservation() {
    const link = this.firstRow.locator("a").first();
    await link.click();
    await this.page.waitForLoadState("networkidle");
  }

  async expectReservationsVisible() {
    await expect(this.reservationRows.first()).toBeVisible();
  }

  async getReservationCodes(): Promise<string[]> {
    return this.reservationRows
      .locator("td")
      .nth(0)
      .allTextContents();
  }
}
