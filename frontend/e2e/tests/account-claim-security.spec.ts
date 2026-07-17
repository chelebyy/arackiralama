import { execFileSync } from "node:child_process";
import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";

const apiBaseUrl = process.env.E2E_API_BASE_URL || "http://localhost:5000";
const locales = ["tr", "en", "ru", "ar", "de"] as const;

function runSql(sql: string) {
  return execFileSync(
    "docker",
    ["exec", "rentacar-postgres", "psql", "-U", "postgres", "-d", "rentacar", "-At", "-v", "ON_ERROR_STOP=1", "-c", sql],
    { encoding: "utf8" }
  ).trim();
}

test.describe.configure({ mode: "serial" });
test.use({ trace: "off", screenshot: "off", video: "off" });

test("public registration and account claim surfaces stay disabled without side effects", async ({ page, request }) => {
  const runId = randomUUID();
  const email = `codex-disabled-membership-${runId.slice(0, 8)}@example.test`;

  const registerResponse = await request.post(`${apiBaseUrl}/api/customer/v1/auth/register`, {
    data: {
      email,
      password: "IgnoredPassword123!",
      fullName: "Disabled Membership",
      phone: "+900000000000",
    },
  });
  expect(registerResponse.status()).toBe(404);

  const trailingSlashRegisterResponse = await request.post(`${apiBaseUrl}/api/customer/v1/auth/register/`, {
    data: { email, password: "IgnoredPassword123!" },
  });
  expect(trailingSlashRegisterResponse.status()).toBe(404);

  const claimResponse = await request.post(`${apiBaseUrl}/api/customer/v1/auth/claim`, {
    data: { token: `disabled-${runId}`, newPassword: "IgnoredPassword123!" },
  });
  expect(claimResponse.status()).toBe(404);

  const trailingSlashClaimResponse = await request.post(`${apiBaseUrl}/api/customer/v1/auth/claim/`, {
    data: { token: `disabled-${runId}`, newPassword: "IgnoredPassword123!" },
  });
  expect(trailingSlashClaimResponse.status()).toBe(404);

  expect(runSql(`SELECT count(*) FROM customers WHERE normalized_email = upper('${email}');`)).toBe("0");
  expect(runSql(`SELECT count(*) FROM background_jobs WHERE payload LIKE '%${email}%';`)).toBe("0");

  for (const locale of locales) {
    const response = await page.goto(`/${locale}/account-claim#token=disabled-${runId}`);
    expect(response?.status()).toBe(404);
  }

  const registerPageResponse = await page.goto("/dashboard/register/v1");
  expect(registerPageResponse?.status()).toBe(404);

  await page.goto("/en");
  await expect(page.locator('a[href="/dashboard/login/v2"]')).toHaveCount(0);
  await expect(page.locator('a[href="/dashboard/login/v1"]')).toHaveCount(0);

  const frontendRegisterResponse = await request.post("/api/auth/register", {
    data: { email, password: "IgnoredPassword123!" },
  });
  expect(frontendRegisterResponse.status()).toBe(404);

  const frontendClaimResponse = await request.post("/api/auth/claim", {
    data: { token: `disabled-${runId}`, newPassword: "IgnoredPassword123!" },
  });
  expect(frontendClaimResponse.status()).toBe(404);
});
