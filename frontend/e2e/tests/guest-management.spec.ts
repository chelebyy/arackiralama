import { expect, test } from "@playwright/test";
import { guestCopy } from "../../lib/guest-reservation-copy";

const initial = {
  publicCode: "SYNTHETIC-REF", status: "Confirmed", version: 7, vehicle: "Test vehicle",
  pickupOffice: "Test office", returnOffice: "Test office",
  pickupDateTime: "2026-11-01T10:00:00Z", returnDateTime: "2026-11-04T10:00:00Z",
  totalAmount: 300, currency: "TRY", canCancel: true, canChangeDates: true,
  cancellationFee: 25, changeFee: 10, csrfToken: "synthetic-csrf",
};

test("pending verification survives reload with its original cooldown and clears after exchange", async ({ page }) => {
  await page.clock.install();
  const copy = guestCopy("en");
  let requests = 0;
  let authenticated = false;
  await page.route("**/api/guest/**", async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/request")) {
      requests++;
      await route.fulfill({ json: { challengeId: "pending-challenge" } });
    } else if (path.endsWith("/verify")) {
      expect(route.request().postDataJSON()).toEqual({ challengeId: "pending-challenge", code: "ORIGINAL-CODE" });
      authenticated = true;
      await route.fulfill({ json: { csrfToken: initial.csrfToken } });
    } else await route.fulfill({ status: authenticated ? 200 : 401, json: authenticated ? initial : {} });
  });
  await page.goto("/en/manage-reservation");
  await page.getByLabel(copy.reference, { exact: true }).fill("REF");
  await page.getByLabel(copy.email, { exact: true }).fill("guest@example.test");
  const send = page.getByRole("button", { name: copy.sendCode, exact: true });
  await send.click();
  await expect(page.getByLabel(copy.code, { exact: true })).toBeVisible();
  await page.clock.fastForward(30000);
  await page.reload();
  await expect(page.getByLabel(copy.code, { exact: true })).toBeVisible();
  await expect(send).toBeDisabled();
  await page.clock.fastForward(31000);
  await expect(send).toBeEnabled();
  await page.reload();
  await expect(send).toBeEnabled();
  await page.getByLabel(copy.code, { exact: true }).fill("ORIGINAL-CODE");
  await page.getByRole("button", { name: copy.verify, exact: true }).click();
  await expect(page.getByText(initial.publicCode, { exact: true })).toBeVisible();
  expect(requests).toBe(1);
  expect(await page.evaluate(() => sessionStorage.getItem("guest-access-challenge"))).toBeNull();
  expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toContain("guest@example.test");
});

test("expired pending verification is removed on reload", async ({ page }) => {
  await page.clock.install();
  const copy = guestCopy("en");
  await page.route("**/api/guest/**", route => route.fulfill({ status: 401, json: {} }));
  await page.goto("/en/manage-reservation");
  await page.evaluate(() => sessionStorage.setItem("guest-access-challenge", JSON.stringify({
    challengeId: "expired-challenge", requestedAt: Date.now() - 601000,
  })));
  await page.reload();
  await expect(page.getByRole("button", { name: copy.sendCode, exact: true })).toBeEnabled();
  await expect(page.getByLabel(copy.code, { exact: true })).toHaveCount(0);
  expect(await page.evaluate(() => sessionStorage.getItem("guest-access-challenge"))).toBeNull();
});

for (const locale of ["tr", "en", "de", "ru", "ar"]) {
  test(`all guest-visible reservation statuses are translated in ${locale}`, async ({ page }) => {
    const copy = guestCopy(locale);
    const statuses = ["Draft", "Hold", "PendingPayment", "Paid", "Active", "Completed", "Cancelled", "Expired", "UnpaidRequest", "Confirmed"] as const;
    let status: typeof statuses[number] = "UnpaidRequest";
    await page.route("**/api/guest/view", route => route.fulfill({ json: {
      ...initial, status, canCancel: false, canChangeDates: false,
    } }));
    for (const next of statuses) {
      status = next;
      await page.goto(`/${locale}/manage-reservation`);
      await expect(page.getByText(`${copy.status}: ${copy[status]}`, { exact: true })).toBeVisible();
    }
  });
}

test("resend cooldown preserves the active challenge and allows retry after a minute", async ({ page }) => {
  await page.clock.install();
  let requests = 0;
  await page.route("**/api/guest/**", async route => {
    if (route.request().url().endsWith("/request")) {
      requests++;
      await route.fulfill({ json: { challengeId: `challenge-${requests}` } });
    } else if (route.request().url().endsWith("/verify")) {
      expect(route.request().postDataJSON().challengeId).toBe("challenge-1");
      await route.fulfill({ status: 401, json: { code: "access_invalid" } });
    } else await route.fulfill({ status: 401, json: { code: "access_invalid" } });
  });
  const copy = guestCopy("en");
  await page.goto("/en/manage-reservation");
  await page.getByLabel(copy.reference, { exact: true }).fill("REF");
  await page.getByLabel(copy.email, { exact: true }).fill("guest@example.test");
  const send = page.getByRole("button", { name: copy.sendCode, exact: true });
  await send.click();
  await expect(send).toBeDisabled();
  await page.getByLabel(copy.email, { exact: true }).press("Enter");
  await page.getByLabel(copy.code, { exact: true }).fill("CODE");
  await page.getByRole("button", { name: copy.verify, exact: true }).click();
  await expect(page.getByRole("status").filter({ hasText: copy.expired })).toBeVisible();
  expect(requests).toBe(1);
  await page.clock.fastForward(61000);
  await expect(send).toBeEnabled();
  await send.click();
  await expect(send).toBeDisabled();
  expect(requests).toBe(2);
});

for (const ending of ["logout", "401", "403"]) {
  test(`reservation consent and inputs reset after ${ending}`, async ({ page }) => {
    const copy = guestCopy("en");
    let authenticated = true;
    let secondReservation = false;
    await page.route("**/api/guest/**", async route => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith("/view")) await route.fulfill({ status: authenticated ? 200 : 401,
        json: { ...initial, publicCode: secondReservation ? "SECOND-REF" : initial.publicCode,
          cancellationFee: secondReservation ? 99 : initial.cancellationFee } });
      else if (path.endsWith("/logout") || path.endsWith("/cancel")) {
        authenticated = false;
        await route.fulfill({ status: ending === "logout" ? 200 : Number(ending), json: {} });
      } else if (path.endsWith("/request")) await route.fulfill({ json: { challengeId: "second-challenge" } });
      else if (path.endsWith("/verify")) {
        authenticated = true; secondReservation = true;
        await route.fulfill({ json: { csrfToken: "new-csrf" } });
      } else await route.fulfill({ json: {} });
    });
    await page.goto("/en/manage-reservation");
    await page.getByLabel(copy.confirmCancel).check();
    await page.getByLabel(copy.pickup, { exact: true }).fill("2026-11-02T10:00");
    await page.getByLabel(copy.returnDate, { exact: true }).fill("2026-11-06T10:00");
    await page.getByLabel(copy.ageAtPickup, { exact: true }).fill("30");
    await page.getByLabel(copy.licenseYearsAtPickup, { exact: true }).fill("8");
    await page.getByLabel(copy.licenseValidThroughReturn).check();
    await page.getByLabel(copy.documentsAvailableAtPickup).check();
    await page.getByRole("button", { name: ending === "logout" ? copy.logout : copy.cancel, exact: true }).click();
    await page.getByLabel(copy.reference, { exact: true }).fill("SECOND-REF");
    await page.getByLabel(copy.email, { exact: true }).fill("second@example.test");
    await page.getByRole("button", { name: copy.sendCode, exact: true }).click();
    await page.getByLabel(copy.code, { exact: true }).fill("SECOND");
    await page.getByRole("button", { name: copy.verify, exact: true }).click();
    await expect(page.getByText("SECOND-REF", { exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: copy.cancel, exact: true })).toBeDisabled();
    for (const field of [copy.confirmCancel, copy.licenseValidThroughReturn, copy.documentsAvailableAtPickup])
      await expect(page.getByLabel(field)).not.toBeChecked();
    for (const field of [copy.pickup, copy.returnDate, copy.ageAtPickup, copy.licenseYearsAtPickup])
      await expect(page.getByLabel(field, { exact: true })).toHaveValue("");
  });
}

for (const locale of ["tr", "en", "de", "ru", "ar"]) {
  test(`guest verification and cancellation in ${locale}`, async ({ page }) => {
    const copy = guestCopy(locale);
    let verified = false;
    let cancelled = false;
    let cancelCount = 0;
    await page.route("**/api/guest/**", async route => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith("/view")) {
        await route.fulfill({ status: verified ? 200 : 401, json: verified ? {
          ...initial, status: cancelled ? "Cancelled" : "Confirmed",
          canCancel: !cancelled, canChangeDates: !cancelled,
        } : { code: "access_invalid" } });
      } else if (path.endsWith("/request")) {
        expect(route.request().postDataJSON()).toMatchObject({ publicCode: "SYNTHETIC-REF", email: "guest@example.test", locale });
        await route.fulfill({ json: { challengeId: "synthetic-challenge" } });
      } else if (path.endsWith("/verify")) {
        expect(route.request().postDataJSON()).toEqual({ challengeId: "synthetic-challenge", code: "SYNTHETIC" });
        verified = true;
        await route.fulfill({ json: { csrfToken: "synthetic-csrf" } });
      } else if (path.endsWith("/cancel")) {
        expect(route.request().postDataJSON()).toEqual({ version: 7, acceptedFee: 25 });
        expect(route.request().headers()["x-guest-csrf"]).toBe("synthetic-csrf");
        cancelCount++; cancelled = true;
        await route.fulfill({ json: { status: "Cancelled" } });
      } else await route.fulfill({ json: {} });
    });
    await page.goto(`/${locale}/manage-reservation`);
    await expect(page.getByRole("heading", { name: copy.title })).toBeVisible();
    await expect(page.locator("main").last()).toHaveAttribute("dir", locale === "ar" ? "rtl" : "ltr");
    await page.getByLabel(copy.reference, { exact: true }).fill("SYNTHETIC-REF");
    await page.getByLabel(copy.email, { exact: true }).fill("guest@example.test");
    await page.getByRole("button", { name: copy.sendCode, exact: true }).click();
    await expect(page.getByRole("button", { name: copy.sendCode, exact: true })).toBeDisabled();
    await page.getByLabel(copy.code, { exact: true }).fill("SYNTHETIC");
    await page.getByRole("button", { name: copy.verify, exact: true }).click();
    await expect(page.getByText("SYNTHETIC-REF", { exact: true })).toBeVisible();
    const cancel = page.getByRole("button", { name: copy.cancel, exact: true });
    await expect(cancel).toBeDisabled();
    await page.getByLabel(copy.confirmCancel).check();
    await cancel.focus();
    await page.keyboard.press("Enter");
    await expect(page.getByText(copy.status + ": " + copy.Cancelled, { exact: true })).toBeVisible();
    expect(cancelCount).toBe(1);
    await page.reload();
    await expect(page.getByText(copy.status + ": " + copy.Cancelled, { exact: true })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    expect(await page.evaluate(() => JSON.stringify(localStorage))).not.toContain("guest@example.test");
  });
}

test("amendment requires reconfirmed declaration, explicit total acceptance and handles expired access", async ({ page }) => {
  const copy = guestCopy("en");
  let quoted = false;
  await page.route("**/api/guest/**", async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/view")) await route.fulfill({ json: initial });
    else if (path.endsWith("/amendment/quote")) {
      expect(route.request().postDataJSON()).toMatchObject({
        pickupDateTimeUtc: "2026-11-02T07:00:00.000Z", returnDateTimeUtc: "2026-11-06T07:00:00.000Z",
        declaration: { ageAtPickup: 30, licenseYearsAtPickup: 8, licenseValidThroughReturn: true, documentsAvailableAtPickup: true },
      });
      quoted = true;
      await route.fulfill({ json: { amendmentId: "offer", expiresAtUtc: "2026-11-01T10:00:00Z",
        rentalTotal: 400, fee: 10, previousFees: 10, finalTotal: 420, difference: 120, currency: "TRY", conditions: { minAge: 21, minLicenseYears: 2 } } });
    } else if (path.endsWith("/amendment/confirm")) {
      expect(quoted).toBe(true);
      expect(route.request().postDataJSON()).toEqual({ amendmentId: "offer", acceptedTotal: 420 });
      await route.fulfill({ status: 401, json: { code: "access_invalid" } });
    }
  });
  await page.goto("/en/manage-reservation");
  await page.getByLabel(copy.pickup, { exact: true }).fill("2026-11-02T10:00");
  await page.getByLabel(copy.returnDate, { exact: true }).fill("2026-11-06T10:00");
  await page.getByLabel(copy.ageAtPickup, { exact: true }).fill("30");
  await page.getByLabel(copy.licenseYearsAtPickup, { exact: true }).fill("8");
  await page.getByLabel(copy.licenseValidThroughReturn).check();
  await page.getByLabel(copy.documentsAvailableAtPickup).check();
  await page.getByRole("button", { name: copy.quote, exact: true }).click();
  await expect(page.getByText(new RegExp(copy.difference))).toBeVisible();
  await expect(page.getByText(new RegExp(copy.previousFees))).toBeVisible();
  await page.getByRole("button", { name: copy.accept, exact: true }).click();
  await expect(page.getByRole("status").filter({ hasText: copy.expired })).toBeVisible();
  await expect(page.getByLabel(copy.email, { exact: true })).toBeVisible();
});
