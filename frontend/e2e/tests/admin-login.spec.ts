/**
 * Admin Login E2E Test
 *
 * Tests admin authentication flow.
 * Requires: Backend running with seeded admin user
 */

import { test, expect, ADMIN_USER } from "../fixtures/test-data";
import { AdminLoginPage } from "../pages/AdminLoginPage";

test.describe("Admin Login", () => {
  test("successful login with valid credentials", async ({ page }) => {
    const loginPage = new AdminLoginPage(page);
    await loginPage.goto();

    await loginPage.login(
      "integration-admin@rentacar.test",
      "IntegrationTestPassword123!"
    );

    await loginPage.expectLoginSuccess();
  });

  test("failed login with wrong password", async ({ page }) => {
    const loginPage = new AdminLoginPage(page);
    await loginPage.goto();

    await loginPage.login(
      "integration-admin@rentacar.test",
      "WrongPassword123!"
    );

    await loginPage.expectLoginError();
  });

  test("failed login with non-existent email", async ({ page }) => {
    const loginPage = new AdminLoginPage(page);
    await loginPage.goto();

    await loginPage.login("nonexistent@test.com", "AnyPassword123!");

    await loginPage.expectLoginError();
  });

  test("login form fields are required", async ({ page }) => {
    const loginPage = new AdminLoginPage(page);
    await loginPage.goto();

    // Submit without filling
    await page.getByRole("button", { name: /giriş|login/i }).click();

    // Should show validation error
    await expect(page.getByText(/zorunlu|required/i)).toBeVisible();
  });
});
