/**
 * Customer Login Page Object
 *
 * Mirrors `AdminLoginPage` for the customer-themed shell at
 * `/dashboard/login/v1`. The form is the same `LoginForm` component
 * with `principalScope="Customer"`, so the backend will reject any
 * Admin credentials submitted here — that's exactly what step 6.1
 * relies on.
 */

import { Page, Locator, expect } from "@playwright/test";

export class CustomerLoginPage {
  readonly page: Page;
  readonly emailInput: Locator;
  readonly passwordInput: Locator;
  readonly submitButton: Locator;
  readonly errorMessage: Locator;

  constructor(page: Page) {
    this.page = page;
    this.emailInput = page.locator("#email");
    this.passwordInput = page.locator("#password");
    this.submitButton = page.getByRole("button", { name: /giriş|login|sign in/i });
    this.errorMessage = page.getByRole("alert");
  }

  async goto() {
    await this.page.goto("/dashboard/login/v1");
  }

  async login(email: string, password: string) {
    await this.emailInput.fill(email);
    await this.passwordInput.fill(password);
    await this.submitButton.click();
  }

  async expectLoginFailure() {
    // Stays on the customer login page — the customer scope never
    // grants admin cookies, so no admin redirect can happen.
    await expect(this.page).toHaveURL(/\/dashboard\/login\/v1/);
  }

  async expectNoAdminCookie() {
    const cookies = await this.page.context().cookies();
    const access = cookies.find((c) => c.name === "rac_access");
    expect(access, "rac_access cookie must not be set after customer login").toBeUndefined();
  }
}
