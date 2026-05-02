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
    this.pickupSelect = page.getByLabel(/pickup|alış/i);
    this.returnSelect = page.getByLabel(/return|dönüş/i);
    this.pickupDateInput = page.getByLabel(/pickup.*date|alış.*tarih/i);
    this.returnDateInput = page.getByLabel(/return.*date|dönüş.*tarih/i);
    this.searchButton = page.getByRole("button", { name: /ara|search|search/i });
  }

  async goto(locale = "tr") {
    await this.page.goto(`/${locale}`);
  }

  async fillSearchForm(data: SearchFormData) {
    if (data.pickupOffice) {
      await this.pickupSelect.selectOption(data.pickupOffice);
    }
    if (data.returnOffice) {
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
