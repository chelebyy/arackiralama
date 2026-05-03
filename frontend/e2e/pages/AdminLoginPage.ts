/**
 * Admin Login Page Object
 *
 * Usage:
 *   const loginPage = new AdminLoginPage(page);
 *   await loginPage.goto();
 *   await loginPage.login('admin@test.com', 'password');
 */

import { Page, Locator, expect } from "@playwright/test";

export class AdminLoginPage {
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
    await this.page.goto("/dashboard/login/v2");
  }

  async login(email: string, password: string) {
    await this.emailInput.fill(email);
    await this.passwordInput.fill(password);
    await this.submitButton.click();
  }

  async expectLoginSuccess() {
    await expect(this.page).toHaveURL(/\/dashboard\/default|\/dashboard$/);
  }

  async expectLoginError(message?: string) {
    if (message) {
      await expect(this.errorMessage).toContainText(message);
    } else {
      await expect(this.errorMessage).toBeVisible();
    }
  }
}
