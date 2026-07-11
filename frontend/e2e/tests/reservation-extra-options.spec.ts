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
});
