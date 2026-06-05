/**
 * Guest & Auth Pages — 6.1 Browser Test Checklist
 *
 * Encodes the manual checklist in
 *   docs/13_Local_Docker_Browser_Test_Checklist.md §6.1
 * as Playwright assertions so the same steps run headlessly on every
 * PR.
 *
 * The backend's Strict rate-limit policy permits only five login
 * attempts per window (see
 *   backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs
 * → AddApiRateLimiting). This file therefore runs serially and reuses
 * one real login (captured in `beforeAll`) for the logout test, so
 * the spec never exceeds the 5-permit budget.
 *
 * Requires the same pre-conditions as the other admin specs:
 *   - docker compose -f backend/docker-compose.yml up -d
 *   - pnpm -C frontend dev (or `pnpm -C frontend start` after build)
 *   - integration-admin@rentacar.test seeded in the DB
 */

import { test as base, expect, ADMIN_USER } from "../fixtures/test-data";
import type { BrowserContext, Page } from "@playwright/test";
import { AdminLoginPage } from "../pages/AdminLoginPage";
import { ForgotPasswordPage } from "../pages/ForgotPasswordPage";
import { ResetPasswordPage } from "../pages/ResetPasswordPage";
import { CustomerLoginPage } from "../pages/CustomerLoginPage";

// Reuse one storage state for the logout test — the rate limiter
// charges every real login, so we only do it once.
const test = base.extend<{ loggedInPage: Page }>({
  loggedInPage: async ({ browser }, use) => {
    const ctx: BrowserContext = await browser.newContext();
    const page = await ctx.newPage();
    const loginPage = new AdminLoginPage(page);
    await loginPage.goto();
    // The login form's `onSubmit` does `fetch('/api/auth/login')` and
    // then `router.push(successRedirect)` — we must wait for both the
    // response and the navigation, otherwise the fixture hands the
    // page back before /dashboard/default has actually loaded.
    const loginResponse = page.waitForResponse(
      (res) => res.url().endsWith("/api/auth/login") && res.request().method() === "POST"
    );
    await loginPage.login(ADMIN_USER.email, ADMIN_USER.password);
    const response = await loginResponse;
    expect(response.ok(), `login API failed: ${response.status()} ${response.statusText()}`).toBe(true);
    await page.waitForURL(/\/dashboard\/default/, { timeout: 15000 });
    await use(page);
    await ctx.close();
  },
});

// Serial keeps the test count low; otherwise a 429 would mask real
// regressions in the same window.
test.describe.configure({ mode: "serial" });

test.describe("6.1.a — Admin login form (v2 shell)", () => {
  test("renders the v2 admin login page", async ({ page }) => {
    const loginPage = new AdminLoginPage(page);
    await loginPage.goto();

    await expect(loginPage.emailInput).toBeVisible();
    await expect(loginPage.passwordInput).toBeVisible();
    await expect(loginPage.submitButton).toBeVisible();
    await expect(page).toHaveURL(/\/dashboard\/login\/v2/);
  });

  test("empty submit triggers native browser validation", async ({ page }) => {
    const loginPage = new AdminLoginPage(page);
    await loginPage.goto();

    await loginPage.submitButton.click();

    // Browser blocks the form — we never leave /dashboard/login/v2.
    await page.waitForTimeout(500);
    await expect(page).toHaveURL(/\/dashboard\/login\/v2/);

    // Both fields report `valueMissing` — the localised message
    // "Lütfen bu alanı doldurun." is the only thing a guest sees.
    const emailValidity = await loginPage.emailInput.evaluate(
      (el) => (el as HTMLInputElement).validity.valueMissing
    );
    const passwordValidity = await loginPage.passwordInput.evaluate(
      (el) => (el as HTMLInputElement).validity.valueMissing
    );
    expect(emailValidity).toBe(true);
    expect(passwordValidity).toBe(true);
  });

  // NOTE: "invalid credentials show a generic error" is covered by
  // admin-login.spec.ts ("failed login with wrong password" +
  // "failed login with non-existent email") — duplicating it here
  // would push us over the backend's 5-permit Strict rate-limit.

  test("valid local admin login redirects to /dashboard/default", async ({ page }) => {
    const loginPage = new AdminLoginPage(page);
    await loginPage.goto();

    // Sanity: the session is anonymous before login.
    const meBefore = await page.request.get("/api/auth/me");
    expect(meBefore.status()).toBe(401);

    const loginResponse = page.waitForResponse(
      (res) => res.url().endsWith("/api/auth/login") && res.request().method() === "POST"
    );
    await loginPage.login(ADMIN_USER.email, ADMIN_USER.password);
    const response = await loginResponse;
    expect(response.ok()).toBe(true);
    await page.waitForURL(/\/dashboard\/default/, { timeout: 15000 });

    // The session is now valid for the backend.
    const meAfter = await page.request.get("/api/auth/me");
    expect(meAfter.ok()).toBe(true);
  });
});

test.describe("6.1.b — Forgot / Reset / Customer / Logout", () => {
  test("/dashboard/forgot-password renders and stays put on success", async ({ page }) => {
    const forgot = new ForgotPasswordPage(page);
    await forgot.goto();

    await expect(forgot.emailInput).toBeVisible();
    await expect(forgot.submitButton).toBeVisible();

    await forgot.requestReset(ADMIN_USER.email, "Admin");
    await page.waitForTimeout(500);
    await forgot.expectStayOnPage();
  });

  test("/dashboard/reset-password renders with no token (form disabled)", async ({ page }) => {
    const reset = new ResetPasswordPage(page);
    await reset.goto(); // no token

    await reset.expectRendered();
    // Native required validation prevents submit without a token.
    const tokenValidity = await reset.tokenInput.evaluate(
      (el) => (el as HTMLInputElement).validity.valueMissing
    );
    expect(tokenValidity).toBe(true);
  });

  test("/dashboard/reset-password rejects a synthetic token", async ({ page }) => {
    const reset = new ResetPasswordPage(page);
    await reset.goto({ token: "synthetic-invalid-token" });

    await reset.expectRendered();
    await reset.submitWithSyntheticToken("NewPassword123!");

    // The form surfaces a generic "Token geçersiz veya süresi dolmuş." toast
    // and never navigates away from /dashboard/reset-password.
    await page.waitForTimeout(1000);
    await expect(page).toHaveURL(/\/dashboard\/reset-password/);
  });

  test("customer login page refuses admin credentials", async ({ page }) => {
    const customer = new CustomerLoginPage(page);
    await customer.goto();

    const loginResponse = page.waitForResponse(
      (res) => res.url().endsWith("/api/auth/login") && res.request().method() === "POST"
    );
    await customer.login(ADMIN_USER.email, ADMIN_USER.password);
    const response = await loginResponse;
    expect(response.status()).toBe(401);

    await customer.expectLoginFailure();
    await customer.expectNoAdminCookie();
  });

  test("customer login page never reaches an admin route", async ({ page }) => {
    const customer = new CustomerLoginPage(page);
    await customer.goto();

    // Direct navigation while unauthenticated — the middleware sends
    // the request back to /dashboard/login/v2, not the customer shell.
    await page.goto("/dashboard/default");
    await expect(page).toHaveURL(/\/dashboard\/login\/v2/);
  });

  test("clearing the session redirects protected routes to the admin login shell", async ({ loggedInPage }) => {
    // Sanity: the shared session is valid for /api/auth/me.
    const meOk = await loggedInPage.request.get("/api/auth/me");
    expect(meOk.ok()).toBe(true);

    // Wipe the session cookies and confirm every protected dashboard
    // route bounces back to /dashboard/login/v2.
    await loggedInPage.context().clearCookies();

    for (const protectedPath of [
      "/dashboard/default",
      "/dashboard/reservations",
      "/dashboard/fleet/vehicles",
    ]) {
      await loggedInPage.goto(protectedPath);
      await expect(loggedInPage).toHaveURL(/\/dashboard\/login\/v2/);
    }
  });
});
