/**
 * Public Homepage / Search Form Page Object
 *
 * Usage:
 *   const homePage = new HomePage(page);
 *   await homePage.goto();
 *   await homePage.fillSearchForm({ pickupOffice: 'alanya', returnOffice: 'antalya' });
 */

import { Page, Locator, expect } from "@playwright/test";

export interface SearchFormData {
  pickupOffice?: string;
  returnOffice?: string;
  pickupDate?: string;
  returnDate?: string;
}

export class HomePage {
  readonly page: Page;
  readonly pickupSelect: Locator;
  readonly returnSelect: Locator;
  readonly pickupDateInput: Locator;
  readonly returnDateInput: Locator;
  readonly searchButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.pickupSelect = page.locator("#pickupLocation");
    this.returnSelect = page.locator("#returnLocation");
    this.pickupDateInput = page.locator("#pickupDate");
    this.returnDateInput = page.locator("#returnDate");
    this.searchButton = page.getByRole("button", { name: /ara|search/i });
  }

  async goto(locale = "tr") {
    await this.page.goto(`/${locale}`);
  }

  async fillSearchForm(data: SearchFormData) {
    await this.page.locator('[data-search-form-hydrated="true"]').waitFor({ state: "attached" });

    if (data.pickupOffice) {
      await this.pickupSelect.selectOption(data.pickupOffice);
    }

    // When returnOffice differs from pickupOffice, we need to disable "same location"
    // to reveal the returnLocation select (which is conditionally rendered)
    if (data.returnOffice && data.returnOffice !== data.pickupOffice) {
      await this.page.getByRole("button", { name: /same|aynı/i }).click();
      // Wait for returnLocation select to be attached to DOM
      await this.returnSelect.waitFor({ state: "attached", timeout: 5000 });
      await this.returnSelect.selectOption(data.returnOffice);
    }

    if (data.pickupDate) {
      await this.pickupDateInput.fill(data.pickupDate);
    }
    if (data.returnDate) {
      await this.returnDateInput.fill(data.returnDate);
    }
  }

  async submitSearch() {
    await this.searchButton.click();
  }

  async expectSearchResults() {
    await this.page.waitForURL(/\/vehicles|\/booking\/step2/);
    await expect(this.page.getByRole("heading", { name: /araç|vehicle/i })).toBeVisible();
  }
}
