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
import type { Page } from "@playwright/test";

async function fillDriverDetails(page: Page) {
  await page.locator("input[name='firstName']").fill("Test");
  await page.locator("input[name='lastName']").fill("User");
  await page.locator("#email").fill("test@example.com");
  await page.locator("#phone").fill("+905551234567");
  await page.locator("#birthDate").fill("1990-01-01");
  await page.locator("#driverLicense").fill("TR12345678");
  await page.locator("#driverLicenseCountry").fill("Turkey");
}

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
      returnOffice: "gzp",
      pickupDate: testDates.pickup,
      returnDate: testDates.returnDate,
    });
    await homePage.submitSearch();

    // Select vehicle
    await page.waitForURL(/\/vehicles|\/booking\/step2|\/araclar/, { timeout: 10000 });
    await page
      .getByRole("link", { name: /hemen rezerve et|book now/i })
      .first()
      .click();

    // Step3 - continue
    await expect(page).toHaveURL(/\/booking\/step3/);
    await fillDriverDetails(page);
    await page.getByRole("button", { name: /devam|continue/i }).click();

    // Step4 - Payment form should be visible
    await expect(page).toHaveURL(/\/booking\/step4/);
    await expect(
      page.getByRole("heading", { name: /ödeme|payment/i, level: 1 })
    ).toBeVisible();
  });

  test("unpaid request requires terms acceptance", async ({ page, testDates }) => {
    const homePage = new HomePage(page);

    // Navigate to step4
    await homePage.goto("tr");
    await homePage.fillSearchForm({
      pickupOffice: "ala",
      returnOffice: "gzp",
      pickupDate: testDates.pickup,
      returnDate: testDates.returnDate,
    });
    await homePage.submitSearch();

    await page.waitForURL(/\/vehicles|\/booking\/step2|\/araclar/, { timeout: 10000 });
    await page
      .getByRole("link", { name: /hemen rezerve et|book now/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/booking\/step3/);
    await fillDriverDetails(page);
    await page.getByRole("button", { name: /devam|continue/i }).click();
    await expect(page).toHaveURL(/\/booking\/step4/);

    const submitButton = page.getByRole("button", {
      name: /talebi gönder|send request/i,
    });
    await expect(submitButton).toBeEnabled();
    await submitButton.click();

    await expect(page.getByText(/kabul etmelisiniz|must accept/i)).toBeVisible();
  });

  test("selected reservation extra appears in the server quote", async ({
    page,
    testDates,
  }) => {
    const homePage = new HomePage(page);
    await homePage.goto("tr");
    await homePage.fillSearchForm({
      pickupOffice: "ala",
      returnOffice: "gzp",
      pickupDate: testDates.pickup,
      returnDate: testDates.returnDate,
    });
    await homePage.submitSearch();

    await page.waitForURL(/\/vehicles|\/booking\/step2|\/araclar/);
    await page
      .getByRole("link", { name: /hemen rezerve et|book now/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/booking\/step3/);
    await page
      .getByRole("button", { name: "Adedi artır Çocuk Koltuğu" })
      .click();
    expect(new URL(page.url()).searchParams.has("extras")).toBe(false);

    await fillDriverDetails(page);
    await page.getByRole("button", { name: /devam|continue/i }).click();

    await expect(page).toHaveURL(/\/booking\/step4/);
    await expect(page.getByText("Çocuk Koltuğu", { exact: true })).toBeVisible();
    await expect(page.getByRole("status")).toContainText(/fiyat teklifi|quote/i);
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
      returnOffice: "gzp",
      pickupDate: testDates.pickup,
      returnDate: testDates.returnDate,
    });
    await homePage.submitSearch();

    await page.waitForURL(/\/vehicles|\/booking\/step2|\/araclar/, { timeout: 10000 });
    await page
      .getByRole("link", { name: /hemen rezerve et|book now/i })
      .first()
      .click();
    await expect(page).toHaveURL(/\/booking\/step3/);

    // Fill customer form (step3)
    await fillDriverDetails(page);
    await page.getByRole("button", { name: /devam|continue/i }).click();

    // Step4 - fill payment form
    await expect(page).toHaveURL(/\/booking\/step4/);

    await page.locator("input[type='checkbox']").check();

    await page
      .getByRole("button", { name: /talebi gönder|send request/i })
      .click();

    // Should redirect to confirmation or 3DS return page
    await page.waitForURL(/\/booking\/(confirmation|3ds-return)/, { timeout: 15000 });
  });
});
