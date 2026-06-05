/**
 * Forgot Password Page Object
 *
 * Encodes the `/dashboard/forgot-password` form for Playwright specs.
 * The form is shared between Admin and Customer scopes — the
 * `#principalScope` select decides which backend endpoint the request
 * hits (`/api/auth/password-reset/request`).
 */

import { Page, Locator, expect } from "@playwright/test";

export class ForgotPasswordPage {
  readonly page: Page;
  readonly principalScope: Locator;
  readonly emailInput: Locator;
  readonly submitButton: Locator;
  readonly successAlert: Locator;

  constructor(page: Page) {
    this.page = page;
    this.principalScope = page.locator("#principalScope");
    this.emailInput = page.locator("#email");
    this.submitButton = page.getByRole("button", { name: /gönder|send/i });
    this.successAlert = page.getByText(/talep alındı|başarıyla|success/i).first();
  }

  async goto() {
    await this.page.goto("/dashboard/forgot-password");
  }

  async requestReset(email: string, scope: "Admin" | "Customer" = "Admin") {
    await this.principalScope.selectOption(scope);
    await this.emailInput.fill(email);
    await this.submitButton.click();
  }

  async expectStayOnPage() {
    await expect(this.page).toHaveURL(/\/dashboard\/forgot-password$/);
  }
}
