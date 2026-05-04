/**
 * Payment Flow (Mock) E2E Test
 *
 * Tests payment flow using MockPaymentProvider.
 * The backend uses MockPaymentProvider by default (appsettings.json).
 * Requires: Backend running with seeded data
 *
 * Note: Full 3DS redirect cannot be tested E2E without real Iyzico.
 * This test verifies the payment form loads and payment intent is created.
 */

import { test, expect } from "../fixtures/test-data";
import { HomePage } from "../pages/HomePage";

test.describe("Payment Flow (Mock)", () => {
  test("step4 payment form loads after vehicle selection", async ({
    page,
    testDates,
  }) => {
    const homePage = new HomePage(page);

    // Navigate to vehicles
    await homePage.goto("tr");
    await homePage.fillSearchForm({
      pickupOffice: "ala",
      returnOffice: "ayt",
      pickupDate: testDates.pickup,
      returnDate: testDates.returnDate,
    });
    await homePage.submitSearch();

    // Select vehicle
    await page.waitForURL(/\/vehicles|\/booking\/step2|\/araclar/, { timeout: 10000 });
    await page
      .locator("[data-testid='vehicle-card']")
      .first()
      .click();

    // Step3 - continue
    await expect(page).toHaveURL(/\/booking\/step3/);
    await page.getByRole("button", { name: /devam|continue/i }).click();

    // Step4 - Payment form should be visible
    await expect(page).toHaveURL(/\/booking\/step4/);
    await expect(
      page.getByRole("heading", { name: /ödeme|payment/i })
    ).toBeVisible();
  });

  test("payment form card number validation", async ({ page, testDates }) => {
    const homePage = new HomePage(page);

    // Navigate to step4
    await homePage.goto("tr");
    await homePage.fillSearchForm({
      pickupOffice: "ala",
      returnOffice: "ayt",
      pickupDate: testDates.pickup,
      returnDate: testDates.returnDate,
    });
    await homePage.submitSearch();

    await page.waitForURL(/\/vehicles|\/booking\/step2|\/araclar/, { timeout: 10000 });
    await page.locator("[data-testid='vehicle-card']").first().click();
    await expect(page).toHaveURL(/\/booking\/step3/);
    await page.getByRole("button", { name: /devam|continue/i }).click();
    await expect(page).toHaveURL(/\/booking\/step4/);

    // Try to submit empty form
    await page.getByRole("button", { name: /ödeme|pay/i }).click();

    // Should show validation errors
    await expect(page.getByText(/zorunlu|required/i)).toBeVisible();
  });

  test("complete mock payment flow creates reservation and redirects", async ({
    page,
    testDates,
  }) => {
    const homePage = new HomePage(page);

    // Navigate to step4 with a vehicle
    await homePage.goto("tr");
    await homePage.fillSearchForm({
      pickupOffice: "ala",
      returnOffice: "ayt",
      pickupDate: testDates.pickup,
      returnDate: testDates.returnDate,
    });
    await homePage.submitSearch();

    await page.waitForURL(/\/vehicles|\/booking\/step2|\/araclar/, { timeout: 10000 });
    await page.locator("[data-testid='vehicle-card']").first().click();
    await expect(page).toHaveURL(/\/booking\/step3/);

    // Fill customer form (step3)
    await page.locator("input[name='firstName']").fill("Test");
    await page.locator("input[name='lastName']").fill("User");
    await page.locator("input[name='email']").fill("test@example.com");
    await page.locator("input[name='phone']").fill("+905551234567");
    await page.locator("input[name='birthDate']").fill("1990-01-01");
    await page.locator("input[name='licenseNumber']").fill("TR12345678");
    await page.locator("input[name='licenseIssueDate']").fill("2015-01-01");
    await page.getByRole("button", { name: /devam|continue/i }).click();

    // Step4 - fill payment form
    await expect(page).toHaveURL(/\/booking\/step4/);

    await page.locator("input[id='cardNumber']").fill("4111111111111111");
    await page.locator("input[id='cardHolder']").fill("Test User");
    await page.locator("input[id='expiryDate']").fill("12/30");
    await page.locator("input[id='cvv']").fill("123");
    await page.locator("input[type='checkbox']").check();

    // Submit payment
    await page.getByRole("button", { name: /complete booking|rezervasyon/i }).click();

    // Should redirect to confirmation or 3DS return page
    await page.waitForURL(/\/booking\/(confirmation|3ds-return)/, { timeout: 15000 });
  });
});
