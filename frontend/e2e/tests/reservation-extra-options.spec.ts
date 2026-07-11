import { test, expect, type Page } from "@playwright/test";
import { ADMIN_USER } from "../fixtures/test-data";
import { AdminLoginPage } from "../pages/AdminLoginPage";

type VehicleGroup = {
  id: string;
  name?: string;
  nameTr?: string;
  nameEn?: string;
  nameDe?: string;
  nameRu?: string;
  nameAr?: string;
};

type AdminExtra = {
  id: string;
  version: number;
  isActive: boolean;
};

const localizedNames = {
  tr: "Bagaj Koruma",
  en: "Luggage Protection",
  de: "Gepäckschutz",
  ru: "Защита багажа",
  ar: "حماية الأمتعة"
} as const;

const localeTabs = {
  tr: "Türkçe",
  en: "İngilizce",
  de: "Almanca",
  ru: "Rusça",
  ar: "Arapça"
} as const;

function unwrapItems(payload: unknown): unknown[] {
  const body = payload as { data?: { items?: unknown[] } | unknown[]; items?: unknown[] };
  if (Array.isArray(body.data)) return body.data;
  if (body.data && !Array.isArray(body.data) && Array.isArray(body.data.items)) {
    return body.data.items;
  }
  return body.items ?? [];
}

async function getAdminGroups(page: Page): Promise<VehicleGroup[]> {
  return page.evaluate(async () => {
    const response = await fetch("/api/admin/v1/vehicle-groups");
    if (!response.ok) throw new Error(`Vehicle groups request failed: ${response.status}`);
    const payload = await response.json();
    const data = payload?.data?.items ?? payload?.data ?? payload?.items ?? payload;
    return Array.isArray(data) ? data : [];
  });
}

async function cleanupExtra(page: Page, search: string) {
  await page.goto("/dashboard/settings/reservation-extras");
  const items = (await page.evaluate(async (query) => {
    const response = await fetch(
      `/api/admin/v1/reservation-extra-options?search=${encodeURIComponent(query)}&includeArchived=true&pageSize=100`
    );
    if (!response.ok) return [];
    const payload = await response.json();
    const data = payload?.data?.items ?? payload?.data ?? payload?.items ?? [];
    return Array.isArray(data) ? data : [];
  }, search)) as AdminExtra[];

  for (const item of items) {
    let version = item.version;
    if (item.isActive) {
      const deactivated = await page.evaluate(
        async ({ id, currentVersion }) => {
          const response = await fetch(`/api/admin/v1/reservation-extra-options/${id}/status`, {
            method: "PATCH",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ version: currentVersion, isActive: false })
          });
          return response.ok ? response.json() : null;
        },
        { id: item.id, currentVersion: version }
      );
      version = deactivated?.data?.version ?? deactivated?.version ?? version;
    }

    await page.evaluate(
      async ({ id, currentVersion }) => {
        await fetch(
          `/api/admin/v1/reservation-extra-options/${id}?version=${encodeURIComponent(String(currentVersion))}`,
          { method: "DELETE" }
        );
      },
      { id: item.id, currentVersion: version }
    );
  }
}

const step4CatalogOption = {
  id: "step4-child-seat",
  code: "child_seat",
  name: "Step 4 Çocuk Koltuğu",
  description: "Step 4 ödeme sınırı test seçeneği",
  unitPrice: 25,
  pricingMode: "PER_DAY",
  maxQuantity: 2,
  iconKey: "baby",
  sortOrder: 1,
  version: 31
};

function step4Quote(quoteId: string, campaignCode?: string) {
  const campaignDiscount = campaignCode ? 37.5 : 0;
  return {
    quoteId,
    expiresAtUtc: "2030-08-10T12:30:00Z",
    dailyRate: 100,
    rentalDays: 3,
    baseTotal: 300,
    extrasTotal: 75,
    campaignDiscount,
    airportFee: 0,
    oneWayFee: 0,
    extraDriverFee: 0,
    childSeatFee: 0,
    youngDriverFee: 0,
    fullCoverageWaiverFee: 0,
    finalTotal: 375 - campaignDiscount,
    currency: "TRY",
    depositAmount: 0,
    preAuthorizationAmount: 0,
    appliedCampaignCode: campaignCode ?? null,
    extraItems: [
      {
        optionId: step4CatalogOption.id,
        optionVersion: step4CatalogOption.version,
        code: step4CatalogOption.code,
        name: step4CatalogOption.name,
        description: step4CatalogOption.description,
        unitPrice: step4CatalogOption.unitPrice,
        pricingMode: step4CatalogOption.pricingMode,
        quantity: 1,
        rentalDays: 3,
        total: 75
      }
    ]
  };
}

async function openStep4WithLegacyExtra(page: Page, vehicleGroupId: string) {
  await page.goto(
    `/tr/booking/step3?pickup=ala&return=gzp&pickupDate=2027-08-10&pickupTime=10%3A00&returnDate=2027-08-13&returnTime=09%3A00&vehicle=${vehicleGroupId}&extras=child_seat`
  );

  await expect(page.getByText(step4CatalogOption.name, { exact: true })).toBeVisible();
  await expect(page.getByText("₺75.00", { exact: true })).toBeVisible();
  await page.locator("#firstName").fill("Payment");
  await page.locator("#lastName").fill("Acceptance");
  await page.locator("#email").fill("payment-acceptance@example.test");
  await page.locator("#phone").fill("+905551234567");
  await page.locator("#birthDate").fill("1990-05-10");
  await page.locator("#driverLicense").fill("PAYMENT-12345");
  await page.locator("#driverLicenseCountry").fill("TR");
  await page.getByRole("button", { name: /ödemeye devam et/i }).click();
  await expect(page).toHaveURL(/\/tr\/booking\/step4\?/);
}

test.describe("Reservation extra options acceptance", () => {
  test("admin authoring enforces readiness and public catalog localizes by assigned group", async ({
    page
  }) => {
    const runId = Date.now().toString();
    const localized = Object.fromEntries(
      Object.entries(localizedNames).map(([locale, name]) => [locale, `${name} ${runId}`])
    ) as Record<keyof typeof localizedNames, string>;

    const loginPage = new AdminLoginPage(page);
    await loginPage.goto();
    await loginPage.login(ADMIN_USER.email, ADMIN_USER.password);
    await loginPage.expectLoginSuccess();

    try {
      await page.goto("/dashboard/settings/reservation-extras");
      await expect(page.getByLabel("Rezervasyon ekstralarında ara")).toBeVisible();

      const groups = await getAdminGroups(page);
      expect(groups.length).toBeGreaterThanOrEqual(2);
      const assignedGroup = groups[0];
      const unassignedGroup = groups[1];
      const assignedGroupLabel =
        assignedGroup.nameTr ?? assignedGroup.nameEn ?? assignedGroup.name ?? assignedGroup.id;

      await page.getByRole("button", { name: "Yeni Ekstra" }).click();
      const dialog = page.getByRole("dialog", { name: "Yeni Rezervasyon Ekstrası" });
      await expect(dialog.getByText("Taslak tamamlanmadı", { exact: true })).toBeVisible();
      await expect(dialog.getByRole("button", { name: "Kaydet ve Aktifleştir" })).toBeDisabled();

      for (const locale of Object.keys(localeTabs) as Array<keyof typeof localeTabs>) {
        await dialog.getByRole("tab", { name: new RegExp(localeTabs[locale]) }).click();
        await dialog.getByLabel(`${localeTabs[locale]} ad`).fill(localized[locale]);
        await dialog
          .getByLabel(`${localeTabs[locale]} açıklama`)
          .fill(`${localized[locale]} test açıklaması`);
      }

      await dialog.getByLabel("Birim fiyat (TRY)").fill("175");
      await dialog.getByLabel("Maksimum adet").fill("3");
      await dialog.getByLabel("Fiyat kuralı").click();
      await page.getByRole("option", { name: "Günlük" }).click();
      await dialog.getByText(assignedGroupLabel, { exact: true }).click();

      await expect(dialog.getByText("Aktivasyona hazır", { exact: true })).toBeVisible();
      await expect(dialog.getByRole("button", { name: "Kaydet ve Aktifleştir" })).toBeEnabled();

      const createResponse = page.waitForResponse(
        (response) =>
          response.url().includes("/api/admin/v1/reservation-extra-options") &&
          response.request().method() === "POST"
      );
      await dialog.getByRole("button", { name: "Kaydet ve Aktifleştir" }).click();
      expect((await createResponse).status()).toBe(200);
      await expect(dialog).toBeHidden();

      const search = page.getByLabel("Rezervasyon ekstralarında ara");
      await search.fill(localized.tr);
      await expect(page.getByText(localized.tr, { exact: true })).toBeVisible();
      await expect(page.getByText("Aktif", { exact: true })).toBeVisible();

      for (const locale of Object.keys(localized) as Array<keyof typeof localized>) {
        const result = await page.evaluate(
          async ({ groupId, localeCode }) => {
            const response = await fetch(
              `http://localhost:5000/api/v1/reservation-extra-options?vehicleGroupId=${groupId}&locale=${localeCode}`
            );
            return {
              status: response.status,
              cacheControl: response.headers.get("cache-control"),
              payload: await response.json()
            };
          },
          { groupId: assignedGroup.id, localeCode: locale }
        );

        expect(result.status).toBe(200);
        expect(result.cacheControl).toContain("no-store");
        const names = unwrapItems(result.payload).map((item) => (item as { name?: string }).name);
        expect(names).toContain(localized[locale]);
      }

      const unassignedResult = await page.evaluate(
        async ({ groupId, localeCode }) => {
          const response = await fetch(
            `http://localhost:5000/api/v1/reservation-extra-options?vehicleGroupId=${groupId}&locale=${localeCode}`
          );
          return response.json();
        },
        { groupId: unassignedGroup.id, localeCode: "tr" }
      );
      const unassignedNames = unwrapItems(unassignedResult).map(
        (item) => (item as { name?: string }).name
      );
      expect(unassignedNames).not.toContain(localized.tr);
    } finally {
      await cleanupExtra(page, runId);
    }
  });

  test("public Step 3 renders per-day and per-rental quantities from the server catalog", async ({
    page
  }) => {
    await page.route("**/api/v1/reservation-extra-options?*", async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 750));
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          items: [
            {
              id: "step3-per-day",
              code: "child_seat",
              name: "Günlük çocuk koltuğu",
              description: "Günlük fiyatlanan test seçeneği",
              unitPrice: 175,
              pricingMode: "PER_DAY",
              maxQuantity: 3,
              iconKey: "baby",
              sortOrder: 1,
              version: 11
            },
            {
              id: "step3-per-rental",
              code: "wifi",
              name: "Kiralama Wi-Fi paketi",
              description: "Kiralama başına fiyatlanan test seçeneği",
              unitPrice: 90,
              pricingMode: "PER_RENTAL",
              maxQuantity: 2,
              iconKey: "wifi",
              sortOrder: 2,
              version: 12
            }
          ]
        })
      });
    });

    await page.goto(
      "/tr/booking/step3?pickup=ala&return=gzp&pickupDate=2026-08-10&pickupTime=10%3A00&returnDate=2026-08-13&returnTime=09%3A00&vehicle=step3-test-group"
    );

    await expect(page.getByLabel("Ek seçenekler yükleniyor...")).toBeVisible();
    await expect(page.getByText("Günlük çocuk koltuğu", { exact: true })).toBeVisible();
    await expect(page.getByText("Kiralama Wi-Fi paketi", { exact: true })).toBeVisible();

    const increasePerDay = page.getByRole("button", {
      name: "Adedi artır Günlük çocuk koltuğu"
    });
    await increasePerDay.click();
    await increasePerDay.click();
    await page.getByRole("button", { name: "Adedi artır Kiralama Wi-Fi paketi" }).click();

    await expect(page.getByText("₺1050.00", { exact: true })).toBeVisible();
    await expect(page.getByText("₺90.00", { exact: true })).toBeVisible();
    await increasePerDay.click();
    await expect(increasePerDay).toBeDisabled();
  });

  test("public Step 3 recovers from a catalog error and exposes the empty state", async ({
    page
  }) => {
    let requestCount = 0;
    await page.route("**/api/v1/reservation-extra-options?*", async (route) => {
      requestCount += 1;
      if (requestCount === 1) {
        await route.fulfill({
          status: 400,
          contentType: "application/json",
          body: JSON.stringify({ message: "Controlled acceptance-test failure" })
        });
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ items: [] })
      });
    });

    await page.goto("/tr/booking/step3?pickup=ala&return=gzp&vehicle=step3-empty-group");

    await expect(
      page.getByRole("alert").filter({
        hasText: "Ek seçenekler yüklenemedi. Lütfen tekrar deneyin."
      })
    ).toBeVisible();
    await page.getByRole("button", { name: "Tekrar dene" }).click();
    await expect(
      page.getByText("Bu araç grubu için aktif ek seçenek bulunmuyor.", { exact: true })
    ).toBeVisible();
    expect(requestCount).toBe(2);
  });

  test("public Step 3 warns about unsupported legacy extras and removes them from the next URL", async ({
    page
  }) => {
    await page.route("**/api/v1/reservation-extra-options?*", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          items: [
            {
              id: "step3-legacy-child-seat",
              code: "child_seat",
              name: "Eski bağlantı çocuk koltuğu",
              description: "Desteklenen eski bağlantı seçeneği",
              unitPrice: 175,
              pricingMode: "PER_DAY",
              maxQuantity: 3,
              iconKey: "baby",
              sortOrder: 1,
              version: 21
            }
          ]
        })
      });
    });

    await page.goto(
      "/tr/booking/step3?pickup=ala&return=gzp&pickupDate=2026-08-10&pickupTime=10%3A00&returnDate=2026-08-13&returnTime=09%3A00&vehicle=step3-legacy-group&extras=child_seat%2Cunsupported_code"
    );

    await expect(
      page.getByText("Eski bağlantıdaki desteklenmeyen ek seçenekler kaldırıldı.", {
        exact: true
      })
    ).toBeVisible();
    await expect(page.getByText("₺525.00", { exact: true })).toBeVisible();

    await page.locator("#firstName").fill("Acceptance");
    await page.locator("#lastName").fill("Tester");
    await page.locator("#email").fill("acceptance@example.test");
    await page.locator("#phone").fill("+905551234567");
    await page.locator("#birthDate").fill("1990-05-10");
    await page.locator("#driverLicense").fill("TEST-12345");
    await page.locator("#driverLicenseCountry").fill("TR");
    await page.getByRole("button", { name: /ödemeye devam et/i }).click();

    await expect(page).toHaveURL(/\/tr\/booking\/step4\?/);
    expect(new URL(page.url()).searchParams.has("extras")).toBe(false);
  });

  test("public Step 4 refreshes the server quote and creates payment only after reservation and hold", async ({
    page
  }) => {
    const quoteRequests: Array<{ body: Record<string, unknown>; sessionId?: string }> = [];
    const order: string[] = [];
    let paidReservationRequest: {
      body: Record<string, unknown>;
      headers: Record<string, string>;
    } | null = null;
    let paymentRequest: Record<string, unknown> | null = null;

    await page.route("**/api/v1/reservation-extra-options?*", (route) =>
      route.fulfill({ json: { items: [step4CatalogOption] } })
    );
    await page.route("**/api/v1/public-site-settings", (route) =>
      route.fulfill({
        json: {
          onlinePaymentEnabled: true,
          paymentMethods: {
            creditCardEnabled: true,
            debitCardEnabled: true,
            unpaidRequestEnabled: true,
            paypalEnabled: false,
            anyEnabled: true
          }
        }
      })
    );
    await page.route("**/api/v1/pricing/quote", async (route) => {
      const body = route.request().postDataJSON() as Record<string, unknown>;
      quoteRequests.push({
        body,
        sessionId: route.request().headers()["x-session-id"]
      });
      const campaignCode = body.campaignCode === "SAVE10" ? "SAVE10" : undefined;
      await route.fulfill({
        json: step4Quote(campaignCode ? "quote-paid-campaign" : "quote-paid-initial", campaignCode)
      });
    });
    await page.route("**/api/v1/pricing/campaigns/validate", (route) =>
      route.fulfill({ json: { valid: true, code: "SAVE10" } })
    );
    await page.route("**/api/v1/reservations", async (route) => {
      order.push("reservation");
      paidReservationRequest = {
        body: route.request().postDataJSON() as Record<string, unknown>,
        headers: route.request().headers()
      };
      await route.fulfill({ json: { id: "paid-reservation", publicCode: "ALN-PAID-E2E" } });
    });
    await page.route("**/api/v1/reservations/paid-reservation/hold", async (route) => {
      order.push("hold");
      await route.fulfill({ json: { id: "paid-reservation", publicCode: "ALN-PAID-E2E" } });
    });
    await page.route("**/api/v1/payments/intents", async (route) => {
      order.push("payment");
      paymentRequest = route.request().postDataJSON() as Record<string, unknown>;
      await route.fulfill({
        json: {
          paymentIntentId: "payment-intent-e2e",
          paymentKind: "Sale",
          status: "Succeeded",
          redirectUrl: null,
          amount: 337.5,
          currency: "TRY",
          expiresAt: "2030-08-10T12:30:00Z",
          transactionId: "transaction-e2e",
          reservationStatus: "Confirmed"
        }
      });
    });

    await openStep4WithLegacyExtra(page, "step4-paid-group");

    await expect(page.getByText(step4CatalogOption.name, { exact: true })).toBeVisible();
    await expect(
      page.getByRole("status").filter({ hasText: /Fiyat teklifi .* geçerlidir/ })
    ).toBeVisible();
    await expect(page.getByText(/375[,.]00/)).toBeVisible();

    await page.getByPlaceholder("Kod girin").fill("save10");
    await page.getByRole("button", { name: "Uygula" }).click();
    await expect(page.getByRole("button", { name: "Uygulandı" })).toBeDisabled();
    await expect.poll(() => quoteRequests.length).toBe(2);
    await expect(page.getByText(/337[,.]50/)).toBeVisible();

    const creditCard = page.getByRole("radio", { name: /Kredi Kartı/ });
    await creditCard.check();
    await page.locator("#cardNumber").fill("4111 1111 1111 1111");
    await page.locator("#cardHolder").fill("Payment Acceptance");
    await page.locator("#expiryDate").fill("12/30");
    await page.locator("#cvv").fill("123");
    await page.getByRole("checkbox").check();
    await page.getByRole("button", { name: "Rezervasyonu Tamamla" }).click();

    await expect(page).toHaveURL(/\/tr\/booking\/confirmation\?.*code=ALN-PAID-E2E/);
    expect(order).toEqual(["reservation", "hold", "payment"]);
    expect(paidReservationRequest).not.toBeNull();
    expect(paidReservationRequest!.body.quoteId).toBe("quote-paid-campaign");
    expect(paidReservationRequest!.headers["x-session-id"]).toBe(quoteRequests.at(-1)?.sessionId);
    expect(paidReservationRequest!.headers["idempotency-key"]).toBeTruthy();
    expect(paymentRequest).toMatchObject({
      reservationId: "paid-reservation",
      paymentMethod: "credit_card"
    });
  });

  test("public Step 4 submits unpaid quote ownership without creating hold or payment intent", async ({
    page
  }) => {
    let quoteSessionId: string | undefined;
    let unpaidRequest: { body: Record<string, unknown>; headers: Record<string, string> } | null =
      null;
    let holdCalls = 0;
    let paymentCalls = 0;

    await page.route("**/api/v1/reservation-extra-options?*", (route) =>
      route.fulfill({ json: { items: [step4CatalogOption] } })
    );
    await page.route("**/api/v1/public-site-settings", (route) =>
      route.fulfill({
        json: {
          onlinePaymentEnabled: true,
          paymentMethods: {
            creditCardEnabled: true,
            debitCardEnabled: true,
            unpaidRequestEnabled: true,
            paypalEnabled: false,
            anyEnabled: true
          }
        }
      })
    );
    await page.route("**/api/v1/pricing/quote", async (route) => {
      quoteSessionId = route.request().headers()["x-session-id"];
      await route.fulfill({ json: step4Quote("quote-unpaid") });
    });
    await page.route("**/api/v1/reservations/unpaid-requests", async (route) => {
      unpaidRequest = {
        body: route.request().postDataJSON() as Record<string, unknown>,
        headers: route.request().headers()
      };
      await route.fulfill({ json: { id: "unpaid-reservation", publicCode: "ALN-UNPAID-E2E" } });
    });
    await page.route("**/api/v1/reservations/*/hold", async (route) => {
      holdCalls += 1;
      await route.fulfill({ status: 500, json: { message: "Unexpected hold call" } });
    });
    await page.route("**/api/v1/payments/intents", async (route) => {
      paymentCalls += 1;
      await route.fulfill({ status: 500, json: { message: "Unexpected payment call" } });
    });

    await openStep4WithLegacyExtra(page, "step4-unpaid-group");

    await page.getByRole("radio", { name: /Online ödeme olmadan talep/ }).check();
    await expect(page.locator("#cardNumber")).toBeHidden();
    await page.getByRole("checkbox").check();
    await page.getByRole("button", { name: "Talebi Gönder" }).click();

    await expect(page).toHaveURL(
      /\/tr\/booking\/confirmation\?.*code=ALN-UNPAID-E2E.*request=unpaid/
    );
    expect(unpaidRequest).not.toBeNull();
    expect(unpaidRequest!.body.quoteId).toBe("quote-unpaid");
    expect(unpaidRequest!.headers["x-session-id"]).toBe(quoteSessionId);
    expect(unpaidRequest!.headers["idempotency-key"]).toBeTruthy();
    expect(holdCalls).toBe(0);
    expect(paymentCalls).toBe(0);
  });
});
