/**
 * Admin Reservation Detail Page Object
 *
 * Usage:
 *   const detailPage = new AdminReservationDetailPage(page);
 *   await detailPage.goto('reservation-id');
 *   await detailPage.clickCancel();
 *   await detailPage.clickRefund();
 */

import { Page, Locator, expect } from "@playwright/test";

export class AdminReservationDetailPage {
  readonly page: Page;
  readonly urlPattern = /\/dashboard\/reservations\/[^/]+$/;
  readonly cancelButton: Locator;
  readonly refundButton: Locator;
  readonly checkInButton: Locator;
  readonly checkOutButton: Locator;
  readonly statusBadge: Locator;

  constructor(page: Page) {
    this.page = page;
    this.cancelButton = page.getByRole("button", { name: /iptal|cancel/i });
    this.refundButton = page.getByRole("button", { name: /iade|refund/i });
    this.checkInButton = page.getByRole("button", { name: /check.in/i });
    this.checkOutButton = page.getByRole("button", { name: /check.out/i });
    this.statusBadge = page.locator("[data-testid='status-badge']");
  }

  async goto(reservationId: string) {
    await this.page.goto(`/dashboard/reservations/${reservationId}`);
    await expect(this.page).toHaveURL(this.urlPattern);
  }

  async clickRefund() {
    await this.refundButton.click();
    // Wait for toast notification
    await this.page.waitForSelector("[data-sonner-toaster]", { state: "visible", timeout: 5000 }).catch(() => {
      // Toast may not appear in all cases
    });
  }

  async clickCancel() {
    await this.cancelButton.click();
  }

  async clickCheckIn() {
    await this.checkInButton.click();
  }

  async clickCheckOut() {
    await this.checkOutButton.click();
  }

  async getStatus(): Promise<string> {
    return this.statusBadge.textContent() ?? "";
  }

  async expectStatus(status: string) {
    await expect(this.statusBadge).toContainText(status);
  }
}
