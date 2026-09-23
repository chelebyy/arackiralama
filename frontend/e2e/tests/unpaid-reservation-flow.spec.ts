import { test, expect, ADMIN_USER } from "../fixtures/test-data";
import { HomePage } from "../pages/HomePage";
import { AdminLoginPage } from "../pages/AdminLoginPage";
import { TrackReservationPage } from "../pages/TrackReservationPage";

test("local unpaid request reaches public tracking and admin detail", async ({ page, baseURL, testDates }) => {
  test.skip(process.env.E2E_UNPAID_ACCEPTANCE !== "true", "Requires an isolated local database with payments disabled.");
  expect(["localhost", "127.0.0.1"]).toContain(new URL(baseURL!).hostname);
  const paymentRequests: string[] = [];
  page.on("request", (request) => {
    if (request.url().includes("/payments/intents") || request.url().endsWith("/hold")) {
      paymentRequests.push(request.url());
    }
  });

  const home = new HomePage(page);
  await home.goto("tr");
  await home.fillSearchForm({ pickupOffice: "ala", returnOffice: "ala", pickupDate: testDates.pickup, returnDate: testDates.returnDate });
  await home.submitSearch();
  await expect(page).toHaveURL(new RegExp(`pickupDate=${testDates.pickup}`));
  await expect(page).toHaveURL(new RegExp(`returnDate=${testDates.returnDate}`));
  await page.getByRole("link", { name: "Hemen Rezerve Et", exact: true }).first().click();
  await expect(page).toHaveURL(/\/booking\/step3/);
  await page.getByLabel("Ad", { exact: true }).fill("Local");
  await page.getByLabel("Soyad", { exact: true }).fill("Validation");
  await page.getByLabel("E-posta", { exact: true }).fill(`local-${Date.now()}@example.test`);
  await page.getByLabel("Telefon", { exact: true }).fill("+905550000001");
  await page.getByLabel("Doğum Tarihi", { exact: true }).fill("1990-01-01");
  await page.getByLabel("Ehliyet No", { exact: true }).fill("LOCAL-TEST-001");
  await page.getByLabel("Ehliyet Ülkesi", { exact: true }).fill("Turkey");
  await page.getByRole("button", { name: /devam/i }).click();
  await expect(page).toHaveURL(/\/booking\/step4/);
  await expect(page.locator("#cardNumber")).toBeHidden();
  await page.getByRole("radio", { name: /Online ödeme olmadan talep/ }).check();
  await page.getByRole("checkbox").check();
  const createdResponse = page.waitForResponse((response) => response.url().endsWith("/reservations/unpaid-requests") && response.request().method() === "POST");
  await page.getByRole("button", { name: "Talebi Gönder", exact: true }).click();
  const response = await createdResponse;
  expect(response.ok()).toBeTruthy();
  const payload = await response.json();
  const reservation = payload.data ?? payload;
  expect(reservation.publicCode).toBeTruthy();
  await expect(page).toHaveURL(/\/booking\/confirmation\?/);
  await expect(page.getByText(reservation.publicCode, { exact: true }).first()).toBeVisible();
  expect(paymentRequests).toEqual([]);

  const tracking = new TrackReservationPage(page);
  await tracking.goto("tr");
  await tracking.searchByCode(reservation.publicCode);
  await expect(page.getByText(reservation.publicCode, { exact: true }).first()).toBeVisible();
  await expect(tracking.errorMessage).toBeHidden();

  const login = new AdminLoginPage(page);
  await login.goto();
  await login.login(ADMIN_USER.email, ADMIN_USER.password);
  await login.expectLoginSuccess();
  await page.goto(`/dashboard/reservations/${reservation.id}`);
  await expect(page.getByText(reservation.publicCode, { exact: true }).first()).toBeVisible();
  await expect(page.getByText("Local Validation", { exact: true }).first()).toBeVisible();
});
