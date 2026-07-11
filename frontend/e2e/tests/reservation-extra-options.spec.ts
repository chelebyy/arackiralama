import { execFileSync } from "node:child_process";
import { type Page } from "@playwright/test";
import { ADMIN_USER, test, expect } from "../fixtures/test-data";
import { AdminLoginPage } from "../pages/AdminLoginPage";
import { HomePage } from "../pages/HomePage";

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
  code: string;
  version: number;
  isActive: boolean;
  unitPrice: number;
  pricingMode: "PER_DAY" | "PER_RENTAL";
  maxQuantity: number;
  iconKey: string;
  sortOrder: number;
  vehicleGroupIds: string[];
  translations: Array<{ locale: string; name: string; description: string }>;
};

type AdminReservationDetail = {
  id: string;
  breakdownSource: "SNAPSHOT" | "LEGACY_TOTAL_ONLY";
  selectedExtras?: Array<{
    optionId: string;
    optionVersion: number;
    name: string;
    unitPrice: number;
    pricingMode: "PER_DAY" | "PER_RENTAL";
    quantity: number;
    rentalDays: number;
    total: number;
  }>;
  priceBreakdown?: {
    baseTotal: number;
    extrasTotal: number;
    campaignDiscount: number;
    airportFee: number;
    oneWayFee: number;
    extraDriverFee: number;
    childSeatFee: number;
    youngDriverFee: number;
    fullCoverageWaiverFee: number;
    finalTotal: number;
  };
};

type QuoteResponse = {
  quoteId: string;
};

type ReservationCreateResponse = {
  id: string;
  publicCode: string;
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

function unwrapData<T>(payload: unknown): T {
  const body = payload as { data?: T };
  return (body.data ?? payload) as T;
}

function runDockerExec(container: string, args: string[]): string {
  return execFileSync("docker", ["exec", container, ...args], {
    encoding: "utf8"
  }).trim();
}

function assertQuoteId(quoteId: string): void {
  expect(quoteId).toMatch(
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i
  );
}

function countReservationsByQuoteId(quoteId: string): number {
  assertQuoteId(quoteId);
  const count = runDockerExec("rentacar-postgres", [
    "psql",
    "-U",
    "postgres",
    "-d",
    "rentacar",
    "-t",
    "-A",
    "-c",
    `select count(*) from reservations where quote_id = '${quoteId}'::uuid;`
  ]);
  return Number.parseInt(count, 10);
}

function cancelReservationInDatabase(reservationId: string): void {
  assertQuoteId(reservationId);
  expect(
    runDockerExec("rentacar-postgres", [
      "psql",
      "-U",
      "postgres",
      "-d",
      "rentacar",
      "-t",
      "-A",
      "-c",
      `update reservations set status = 'Cancelled', unpaid_request_expires_at_utc = null where id = '${reservationId}'::uuid returning 1;`
    ])
  ).toContain("1");
}

function expireQuote(quoteId: string): void {
  assertQuoteId(quoteId);
  expect(
    runDockerExec("rentacar-redis", [
      "redis-cli",
      "PEXPIRE",
      `reservation_quote:${quoteId.replaceAll("-", "")}`,
      "1"
    ])
  ).toBe("1");
}

function cleanupQuoteKeys(quoteId: string): void {
  assertQuoteId(quoteId);
  const compactId = quoteId.replaceAll("-", "");
  runDockerExec("rentacar-redis", [
    "redis-cli",
    "DEL",
    `reservation_quote:${compactId}`,
    `reservation_quote_claim:${compactId}`,
    `reservation_quote_consumed:${compactId}`
  ]);
}

async function getAdminExtraByCode(page: Page, code: string): Promise<AdminExtra> {
  return page.evaluate(async (optionCode) => {
    const response = await fetch(
      `/api/admin/v1/reservation-extra-options?search=${encodeURIComponent(optionCode)}&includeArchived=true&pageSize=100`
    );
    if (!response.ok) throw new Error(`Reservation extras request failed: ${response.status}`);
    const payload = await response.json();
    const items = payload?.data?.items ?? payload?.items ?? [];
    const option = items.find((item: { code?: string }) => item.code === optionCode);
    if (!option) throw new Error(`Reservation extra not found: ${optionCode}`);
    return option;
  }, code);
}

async function updateAdminExtra(
  page: Page,
  option: AdminExtra,
  changes: Partial<Pick<AdminExtra, "unitPrice" | "translations">>
): Promise<AdminExtra> {
  return page.evaluate(
    async ({ item, patch }) => {
      const response = await fetch(`/api/admin/v1/reservation-extra-options/${item.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          version: item.version,
          unitPrice: patch.unitPrice ?? item.unitPrice,
          pricingMode: item.pricingMode,
          maxQuantity: item.maxQuantity,
          iconKey: item.iconKey,
          sortOrder: item.sortOrder,
          vehicleGroupIds: item.vehicleGroupIds,
          translations: patch.translations ?? item.translations
        })
      });
      if (!response.ok) throw new Error(`Reservation extra update failed: ${response.status}`);
      const payload = await response.json();
      return payload?.data ?? payload;
    },
    { item: option, patch: changes }
  );
}

async function getAdminReservation(page: Page, id: string): Promise<AdminReservationDetail> {
  return page.evaluate(async (reservationId) => {
    const response = await fetch(`/api/admin/v1/reservations/${reservationId}`);
    if (!response.ok) throw new Error(`Reservation detail request failed: ${response.status}`);
    const payload = await response.json();
    return payload?.data ?? payload;
  }, id);
}

async function findLegacyReservation(page: Page): Promise<AdminReservationDetail> {
  return page.evaluate(async () => {
    let pageNumber = 1;
    let totalPages = 1;
    let totalCount = 0;
    let inspectedCount = 0;
    const observedSources = new Set<string>();
    do {
      const listResponse = await fetch(
        `/api/admin/v1/reservations?page=${pageNumber}&pageSize=100`
      );
      if (!listResponse.ok) {
        throw new Error(`Reservation list request failed: ${listResponse.status}`);
      }
      const listPayload = await listResponse.json();
      const list = listPayload?.data ?? listPayload;
      const items = Array.isArray(list) ? list : (list?.items ?? []);
      totalCount = Array.isArray(list) ? items.length : (list?.totalCount ?? items.length);
      totalPages = Array.isArray(list)
        ? 1
        : (list?.totalPages ?? Math.max(1, Math.ceil(totalCount / (list?.pageSize ?? 100))));
      for (const item of items) {
        const detailResponse = await fetch(`/api/admin/v1/reservations/${item.id}`);
        if (!detailResponse.ok) continue;
        const detailPayload = await detailResponse.json();
        const detail = detailPayload?.data ?? detailPayload;
        inspectedCount += 1;
        observedSources.add(String(detail.breakdownSource));
        if (detail.breakdownSource === "LEGACY_TOTAL_ONLY") return detail;
      }
      pageNumber += 1;
    } while (pageNumber <= totalPages);
    throw new Error(
      `No LEGACY_TOTAL_ONLY reservation is available: total=${totalCount}, pages=${totalPages}, inspected=${inspectedCount}, sources=${Array.from(observedSources).join(",")}`
    );
  });
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

function step4QuoteWithoutExtras(quoteId: string) {
  return {
    ...step4Quote(quoteId),
    extrasTotal: 0,
    finalTotal: 300,
    extraItems: []
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

  test("public Step 4 keeps an unexpired quote across a price-only catalog change", async ({
    page
  }) => {
    let catalogCalls = 0;
    let quoteCalls = 0;
    const reservationQuoteIds: unknown[] = [];

    await page.route("**/api/v1/reservation-extra-options?*", (route) => {
      catalogCalls += 1;
      const option =
        catalogCalls === 1
          ? step4CatalogOption
          : { ...step4CatalogOption, unitPrice: 40, version: 32 };
      return route.fulfill({ json: { items: [option] } });
    });
    await page.route("**/api/v1/public-site-settings", (route) =>
      route.fulfill({
        json: {
          onlinePaymentEnabled: true,
          paymentMethods: {
            creditCardEnabled: true,
            debitCardEnabled: false,
            unpaidRequestEnabled: false,
            paypalEnabled: false,
            anyEnabled: true
          }
        }
      })
    );
    await page.route("**/api/v1/pricing/quote", async (route) => {
      quoteCalls += 1;
      await route.fulfill({ json: step4Quote("quote-before-price-change") });
    });
    await page.route("**/api/v1/reservations", async (route) => {
      reservationQuoteIds.push((route.request().postDataJSON() as Record<string, unknown>).quoteId);
      await route.fulfill({ json: { id: "price-only-reservation", publicCode: "ALN-PRICE-E2E" } });
    });
    await page.route("**/api/v1/reservations/price-only-reservation/hold", (route) =>
      route.fulfill({ json: { id: "price-only-reservation", publicCode: "ALN-PRICE-E2E" } })
    );
    await page.route("**/api/v1/payments/intents", (route) =>
      route.fulfill({
        json: {
          paymentIntentId: "price-only-payment",
          status: "Succeeded",
          redirectUrl: null
        }
      })
    );

    await openStep4WithLegacyExtra(page, "step4-price-only-group");
    await expect.poll(() => quoteCalls).toBe(1);
    await page.locator("#cardNumber").fill("4111 1111 1111 1111");
    await page.locator("#cardHolder").fill("Price Promise");
    await page.locator("#expiryDate").fill("12/30");
    await page.locator("#cvv").fill("123");
    await page.getByRole("checkbox").check();
    await page.getByRole("button", { name: "Rezervasyonu Tamamla" }).click();

    await expect(page).toHaveURL(/\/tr\/booking\/confirmation\?.*code=ALN-PRICE-E2E/);
    expect(reservationQuoteIds).toEqual(["quote-before-price-change"]);
    expect(catalogCalls).toBe(1);
    expect(quoteCalls).toBe(1);
  });

  test("public Step 4 bounds availability conflict recovery and preserves payment form state", async ({
    page
  }) => {
    let catalogCalls = 0;
    let quoteCalls = 0;
    const reservationRequests: Array<{ quoteId: unknown; idempotencyKey?: string }> = [];

    await page.route("**/api/v1/reservation-extra-options?*", (route) => {
      catalogCalls += 1;
      return route.fulfill({ json: { items: catalogCalls === 1 ? [step4CatalogOption] : [] } });
    });
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
      quoteCalls += 1;
      const quote =
        quoteCalls === 1
          ? step4Quote("quote-before-availability-change")
          : step4QuoteWithoutExtras(`quote-after-availability-change-${quoteCalls}`);
      await route.fulfill({ json: quote });
    });
    await page.route("**/api/v1/reservations", async (route) => {
      const body = route.request().postDataJSON() as Record<string, unknown>;
      reservationRequests.push({
        quoteId: body.quoteId,
        idempotencyKey: route.request().headers()["idempotency-key"]
      });
      if (reservationRequests.length <= 2) {
        await route.fulfill({
          status: 409,
          json: { statusCode: 409, code: "CONFLICT", message: "Quote availability changed" }
        });
        return;
      }
      await route.fulfill({
        json: { id: "confirmed-reservation", publicCode: "ALN-CONFLICT-E2E" }
      });
    });
    await page.route("**/api/v1/reservations/confirmed-reservation/hold", (route) =>
      route.fulfill({ json: { id: "confirmed-reservation", publicCode: "ALN-CONFLICT-E2E" } })
    );
    await page.route("**/api/v1/payments/intents", (route) =>
      route.fulfill({
        json: {
          paymentIntentId: "confirmed-payment",
          status: "Succeeded",
          redirectUrl: null
        }
      })
    );

    await openStep4WithLegacyExtra(page, "step4-availability-group");
    await page.getByRole("radio", { name: /Banka Kartı/ }).check();
    await page.locator("#cardNumber").fill("4111 1111 1111 1111");
    await page.locator("#cardHolder").fill("Conflict Survivor");
    await page.locator("#expiryDate").fill("12/30");
    await page.locator("#cvv").fill("123");
    await page.getByRole("checkbox").check();
    await page.getByRole("button", { name: "Rezervasyonu Tamamla" }).click();

    const conflictAlert = page.getByRole("alert").filter({
      hasText: "Ek seçenekler değişti. Güncel teklifi inceleyip yeniden onaylayın."
    });
    await expect(conflictAlert).toBeVisible();
    await expect(page.getByText(step4CatalogOption.name, { exact: true })).toBeHidden();
    await expect(page.getByText(/300[,.]00/).last()).toBeVisible();
    await expect(page.getByRole("radio", { name: /Banka Kartı/ })).toBeChecked();
    await expect(page.locator("#cardNumber")).toHaveValue("4111 1111 1111 1111");
    await expect(page.locator("#cardHolder")).toHaveValue("Conflict Survivor");
    await expect(page.locator("#expiryDate")).toHaveValue("12/30");
    await expect(page.locator("#cvv")).toHaveValue("123");
    await expect(page.getByRole("checkbox")).toBeChecked();
    expect(reservationRequests).toHaveLength(2);
    expect(reservationRequests[0].idempotencyKey).toBe(reservationRequests[1].idempotencyKey);
    await expect.poll(() => quoteCalls).toBe(2);

    await conflictAlert.getByRole("button", { name: "Teklifi yenile" }).click();
    await expect.poll(() => quoteCalls).toBe(3);
    expect(reservationRequests).toHaveLength(2);
    await page.getByRole("button", { name: "Rezervasyonu Tamamla" }).click();

    await expect(page).toHaveURL(/\/tr\/booking\/confirmation\?.*code=ALN-CONFLICT-E2E/);
    expect(reservationRequests).toHaveLength(3);
    expect(reservationRequests[2].quoteId).toBe("quote-after-availability-change-3");
    expect(reservationRequests[2].idempotencyKey).not.toBe(reservationRequests[1].idempotencyKey);
  });

  test("expired, cross-session, and replayed quotes create at most one reservation", async ({
    page,
    testDates
  }) => {
    const quoteIds: string[] = [];
    let createdReservationId: string | undefined;

    try {
      const pickupDate = new Date(`${testDates.pickup}T00:00:00Z`);
      pickupDate.setUTCDate(pickupDate.getUTCDate() + 45);
      const returnDate = new Date(pickupDate);
      returnDate.setUTCDate(returnDate.getUTCDate() + 3);
      const homePage = new HomePage(page);
      await homePage.goto("tr");
      await homePage.fillSearchForm({
        pickupOffice: "ala",
        returnOffice: "gzp",
        pickupDate: pickupDate.toISOString().split("T")[0],
        returnDate: returnDate.toISOString().split("T")[0]
      });
      await homePage.submitSearch();

      await page.waitForURL(/\/vehicles|\/booking\/step2|\/araclar/);
      await page
        .getByRole("link", { name: /hemen rezerve et|book now/i })
        .first()
        .click();
      await expect(page).toHaveURL(/\/booking\/step2/);
      const step3Url = new URL(page.url());
      step3Url.pathname = step3Url.pathname.replace("/booking/step2", "/booking/step3");
      await page.goto(step3Url.toString());
      await expect(page).toHaveURL(/\/booking\/step3/);
      await page.getByRole("button", { name: "Adedi artır Çocuk Koltuğu" }).click();

      await page.locator("#firstName").fill("Quote");
      await page.locator("#lastName").fill("Replay");
      await page.locator("#email").fill(`quote-replay-${Date.now()}@example.test`);
      await page.locator("#phone").fill("+905551234567");
      await page.locator("#birthDate").fill("1990-05-10");
      await page.locator("#driverLicense").fill(`QUOTE-${Date.now()}`);
      await page.locator("#driverLicenseCountry").fill("TR");
      const initialQuoteResponsePromise = page.waitForResponse(
        (response) =>
          response.url().includes("/api/v1/pricing/quote") && response.request().method() === "POST"
      );
      await page.getByRole("button", { name: /devam|continue/i }).click();
      const initialQuoteResponse = await initialQuoteResponsePromise;
      const initialQuotePayload = await initialQuoteResponse.json();
      expect(initialQuoteResponse.status(), JSON.stringify(initialQuotePayload)).toBe(200);
      const initialQuote = unwrapData<QuoteResponse>(initialQuotePayload);
      assertQuoteId(initialQuote.quoteId);
      quoteIds.push(initialQuote.quoteId);
      const apiOrigin = new URL(initialQuoteResponse.url()).origin;

      const quoteRequest = initialQuoteResponse.request().postDataJSON() as Record<string, unknown>;
      const sessionId = initialQuoteResponse.request().headers()["x-session-id"];
      expect(sessionId).toBeTruthy();
      const reservationInputs = { ...quoteRequest };
      delete reservationInputs.selectedExtras;
      const reservationRequest = {
        ...reservationInputs,
        customer: {
          firstName: "Quote",
          lastName: "Replay",
          email: `quote-replay-api-${Date.now()}@example.test`,
          phone: "+90 555 123 45 67"
        }
      };

      const createQuote = async (): Promise<QuoteResponse> => {
        const result = await page.evaluate(
          async ({ body, ownerSession, origin }) => {
            const response = await fetch(`${origin}/api/v1/pricing/quote`, {
              method: "POST",
              headers: {
                "Content-Type": "application/json",
                "X-Session-Id": ownerSession
              },
              body: JSON.stringify(body)
            });
            return { status: response.status, payload: await response.json() };
          },
          { body: quoteRequest, ownerSession: sessionId!, origin: apiOrigin }
        );
        expect(result.status, JSON.stringify(result.payload)).toBe(200);
        const quote = unwrapData<QuoteResponse>(result.payload);
        assertQuoteId(quote.quoteId);
        quoteIds.push(quote.quoteId);
        return quote;
      };

      const createReservation = async (
        quoteId: string,
        ownerSession: string,
        idempotencyKey: string
      ) =>
        page.evaluate(
          async ({ body, currentQuoteId, currentSession, currentIdempotencyKey, origin }) => {
            const response = await fetch(`${origin}/api/v1/reservations`, {
              method: "POST",
              headers: {
                "Content-Type": "application/json",
                "X-Session-Id": currentSession,
                "Idempotency-Key": currentIdempotencyKey
              },
              body: JSON.stringify({ ...body, quoteId: currentQuoteId })
            });
            return { status: response.status, payload: await response.json() };
          },
          {
            body: reservationRequest,
            currentQuoteId: quoteId,
            currentSession: ownerSession,
            currentIdempotencyKey: idempotencyKey,
            origin: apiOrigin
          }
        );

      const crossSessionQuote = await createQuote();
      const crossSessionResult = await createReservation(
        crossSessionQuote.quoteId,
        `cross-session-${Date.now()}`,
        `cross-session-${Date.now()}`
      );
      expect(crossSessionResult.status, JSON.stringify(crossSessionResult.payload)).toBe(409);
      expect(countReservationsByQuoteId(crossSessionQuote.quoteId)).toBe(0);

      const expiredQuote = await createQuote();
      expireQuote(expiredQuote.quoteId);
      await expect
        .poll(() =>
          runDockerExec("rentacar-redis", [
            "redis-cli",
            "EXISTS",
            `reservation_quote:${expiredQuote.quoteId.replaceAll("-", "")}`
          ])
        )
        .toBe("0");
      const expiredResult = await createReservation(
        expiredQuote.quoteId,
        sessionId!,
        `expired-${Date.now()}`
      );
      expect(expiredResult.status, JSON.stringify(expiredResult.payload)).toBe(409);
      expect(countReservationsByQuoteId(expiredQuote.quoteId)).toBe(0);

      const firstResult = await createReservation(
        initialQuote.quoteId,
        sessionId!,
        `replay-first-${Date.now()}`
      );
      expect(firstResult.status, JSON.stringify(firstResult.payload)).toBe(200);
      const firstReservation = unwrapData<ReservationCreateResponse>(firstResult.payload);
      createdReservationId = firstReservation.id;

      const replayResult = await createReservation(
        initialQuote.quoteId,
        sessionId!,
        `replay-second-${Date.now()}`
      );
      expect(replayResult.status, JSON.stringify(replayResult.payload)).toBe(200);
      const replayReservation = unwrapData<ReservationCreateResponse>(replayResult.payload);
      expect(replayReservation.id).toBe(firstReservation.id);
      expect(countReservationsByQuoteId(initialQuote.quoteId)).toBe(1);
    } finally {
      if (createdReservationId) cancelReservationInDatabase(createdReservationId);

      for (const quoteId of quoteIds) cleanupQuoteKeys(quoteId);
    }
  });

  test("admin detail preserves selected-extra history and marks legacy totals explicitly", async ({
    page,
    testDates
  }) => {
    let createdReservationId: string | undefined;
    let originalOption: AdminExtra | undefined;
    let updatedOption: AdminExtra | undefined;
    let adminAuthenticated = false;

    try {
      const pickupDate = new Date(`${testDates.pickup}T00:00:00Z`);
      pickupDate.setUTCDate(pickupDate.getUTCDate() + 30);
      const returnDate = new Date(pickupDate);
      returnDate.setUTCDate(returnDate.getUTCDate() + 3);
      const homePage = new HomePage(page);
      await homePage.goto("tr");
      await homePage.fillSearchForm({
        pickupOffice: "ala",
        returnOffice: "gzp",
        pickupDate: pickupDate.toISOString().split("T")[0],
        returnDate: returnDate.toISOString().split("T")[0]
      });
      await homePage.submitSearch();

      await page.waitForURL(/\/vehicles|\/booking\/step2|\/araclar/);
      await page
        .getByRole("link", { name: /hemen rezerve et|book now/i })
        .first()
        .click();
      await expect(page).toHaveURL(/\/booking\/step2/);
      const step3Url = new URL(page.url());
      step3Url.pathname = step3Url.pathname.replace("/booking/step2", "/booking/step3");
      await page.goto(step3Url.toString());
      await expect(page).toHaveURL(/\/booking\/step3/);
      await page.getByRole("button", { name: "Adedi artır Çocuk Koltuğu" }).click();

      await page.locator("#firstName").fill("Snapshot");
      await page.locator("#lastName").fill("Acceptance");
      await page.locator("#email").fill(`snapshot-${Date.now()}@example.test`);
      await page.locator("#phone").fill("+905551234567");
      await page.locator("#birthDate").fill("1990-05-10");
      await page.locator("#driverLicense").fill(`SNAPSHOT-${Date.now()}`);
      await page.locator("#driverLicenseCountry").fill("TR");
      await page.getByRole("button", { name: /devam|continue/i }).click();

      await expect(page).toHaveURL(/\/booking\/step4/);
      await expect(page.getByText("Çocuk Koltuğu", { exact: true })).toBeVisible();
      await page.getByRole("radio", { name: /Online ödeme olmadan talep/ }).check();
      await page.getByRole("checkbox").check();
      const createResponsePromise = page.waitForResponse(
        (response) =>
          response.url().includes("/api/v1/reservations/unpaid-requests") &&
          response.request().method() === "POST"
      );
      await page.getByRole("button", { name: "Talebi Gönder" }).click();
      const createResponse = await createResponsePromise;
      const createPayload = await createResponse.json();
      expect(createResponse.status(), JSON.stringify(createPayload)).toBe(200);
      const createdReservation = unwrapData<{ id: string; publicCode: string }>(createPayload);
      createdReservationId = createdReservation.id;
      await expect(page).toHaveURL(
        new RegExp(`/tr/booking/confirmation\\?.*code=${createdReservation.publicCode}`)
      );

      const loginPage = new AdminLoginPage(page);
      await loginPage.goto();
      await loginPage.login(ADMIN_USER.email, ADMIN_USER.password);
      await loginPage.expectLoginSuccess();
      adminAuthenticated = true;

      const originalDetail = await getAdminReservation(page, createdReservationId);
      expect(originalDetail.breakdownSource).toBe("SNAPSHOT");
      expect(originalDetail.selectedExtras).toHaveLength(1);
      const selectedSnapshot = originalDetail.selectedExtras![0];
      expect(selectedSnapshot.name).toBe("Çocuk Koltuğu");
      expect(selectedSnapshot.pricingMode).toBe("PER_DAY");
      expect(selectedSnapshot.quantity).toBe(1);
      expect(selectedSnapshot.total).toBe(
        selectedSnapshot.unitPrice * selectedSnapshot.quantity * selectedSnapshot.rentalDays
      );

      const originalBreakdown = originalDetail.priceBreakdown!;
      expect(originalBreakdown.extrasTotal).toBe(selectedSnapshot.total);
      expect(originalBreakdown.finalTotal).toBe(
        originalBreakdown.baseTotal +
          originalBreakdown.extrasTotal +
          originalBreakdown.airportFee +
          originalBreakdown.oneWayFee +
          originalBreakdown.extraDriverFee +
          originalBreakdown.childSeatFee +
          originalBreakdown.youngDriverFee +
          originalBreakdown.fullCoverageWaiverFee -
          originalBreakdown.campaignDiscount
      );

      await page.goto(`/dashboard/reservations/${createdReservationId}`);
      await expect(page.getByText(selectedSnapshot.name, { exact: true })).toBeVisible();
      await expect(
        page.getByText(`${selectedSnapshot.quantity} adet`, { exact: false })
      ).toBeVisible();
      await expect(page.getByText("Ekstralar", { exact: true })).toBeVisible();
      await expect(page.getByText("Toplam", { exact: true })).toBeVisible();

      originalOption = await getAdminExtraByCode(page, "child_seat");
      const changedName = `Çocuk Koltuğu Güncel ${Date.now()}`;
      updatedOption = await updateAdminExtra(page, originalOption, {
        unitPrice: originalOption.unitPrice + 25,
        translations: originalOption.translations.map((translation) =>
          translation.locale === "tr" ? { ...translation, name: changedName } : translation
        )
      });

      const detailAfterCatalogChange = await getAdminReservation(page, createdReservationId);
      expect(detailAfterCatalogChange.selectedExtras).toEqual(originalDetail.selectedExtras);
      expect(detailAfterCatalogChange.priceBreakdown).toEqual(originalDetail.priceBreakdown);
      await page.reload();
      await expect(page.getByText(selectedSnapshot.name, { exact: true })).toBeVisible();
      await expect(page.getByText(changedName, { exact: true })).toBeHidden();

      const legacyReservation = await findLegacyReservation(page);
      await page.goto(`/dashboard/reservations/${legacyReservation.id}`);
      await expect(
        page.getByText("Eski rezervasyon: yalnızca toplam tutar kaydı bulunuyor.", {
          exact: true
        })
      ).toBeVisible();
    } finally {
      if ((createdReservationId || updatedOption) && !adminAuthenticated) {
        const loginPage = new AdminLoginPage(page);
        await loginPage.goto();
        await loginPage.login(ADMIN_USER.email, ADMIN_USER.password);
        await loginPage.expectLoginSuccess();
        adminAuthenticated = true;
      }

      if (updatedOption && originalOption) {
        await updateAdminExtra(page, updatedOption, {
          unitPrice: originalOption.unitPrice,
          translations: originalOption.translations
        });
      }

      if (createdReservationId && adminAuthenticated) {
        await page.evaluate(async (reservationId) => {
          const response = await fetch(`/api/admin/v1/reservations/${reservationId}/cancel`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify("Reservation extra immutable-history acceptance cleanup")
          });
          if (!response.ok) throw new Error(`Reservation cleanup failed: ${response.status}`);
        }, createdReservationId);
      }
    }
  });
});
