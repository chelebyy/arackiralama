/**
 * Reset Password Page Object
 *
 * Encodes the `/dashboard/reset-password` form for Playwright specs.
 * The form needs a non-empty `?token=...` query param to be useful —
 * without a token the native `required` validation disables the form.
 */

import { Page, Locator, expect } from "@playwright/test";

export class ResetPasswordPage {
  readonly page: Page;
  readonly principalScope: Locator;
  readonly tokenInput: Locator;
  readonly newPasswordInput: Locator;
  readonly confirmPasswordInput: Locator;
  readonly submitButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.principalScope = page.locator("#principalScope");
    this.tokenInput = page.locator("#token");
    this.newPasswordInput = page.locator("#newPassword");
    this.confirmPasswordInput = page.locator("#confirmPassword");
    this.submitButton = page.getByRole("button", { name: /reset password|sıfırla/i });
  }

  async goto(options: { token?: string; scope?: "admin" | "customer" } = {}) {
    const params = new URLSearchParams();
    if (options.token !== undefined) params.set("token", options.token);
    if (options.scope) params.set("scope", options.scope);

    const suffix = params.toString() ? `?${params.toString()}` : "";
    await this.page.goto(`/dashboard/reset-password${suffix}`);
  }

  async expectRendered() {
    await expect(this.tokenInput).toBeVisible();
    await expect(this.newPasswordInput).toBeVisible();
    await expect(this.confirmPasswordInput).toBeVisible();
  }

  async submitWithSyntheticToken(newPassword: string) {
    await this.tokenInput.fill("synthetic-invalid-token");
    await this.newPasswordInput.fill(newPassword);
    await this.confirmPasswordInput.fill(newPassword);
    await this.submitButton.click();
  }
}
