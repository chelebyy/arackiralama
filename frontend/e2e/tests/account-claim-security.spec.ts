import { execFileSync } from "node:child_process";
import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";

const apiBaseUrl = process.env.E2E_API_BASE_URL || "http://localhost:5000";
const password = "CodexClaimPassword123!";

const localeExpectations = [
  { locale: "tr", title: "Hesap Sahiplenme", submit: "Parolayi Olustur" },
  { locale: "en", title: "Account Claim", submit: "Set Password" },
  { locale: "ru", title: "Принадлежность аккаунта", submit: "Установить пароль" },
  { locale: "ar", title: "إدعاء الحساب", submit: "تعيين كلمة المرور" },
  { locale: "de", title: "Konto beanspruchen", submit: "Passwort festlegen" },
] as const;

function runSql(sql: string) {
  return execFileSync(
    "docker",
    ["exec", "rentacar-postgres", "psql", "-U", "postgres", "-d", "rentacar", "-At", "-v", "ON_ERROR_STOP=1", "-c", sql],
    { encoding: "utf8" }
  ).trim();
}

test.describe.configure({ mode: "serial" });
test.use({ trace: "off", screenshot: "off", video: "off" });

test("account claim is localized, single-use, and creates a working customer credential", async ({ page, request }) => {
  const customerId = randomUUID();
  const email = `codex-claim-${customerId.slice(0, 8)}@example.test`;
  const normalizedEmail = email.toUpperCase();

  runSql(`
    INSERT INTO customers (
      id, full_name, phone, email, license_year, identity_number, nationality,
      created_at, updated_at, failed_login_count, normalized_email, token_version
    ) VALUES (
      '${customerId}', 'Codex Claim', '', '${email}', 0, '', 'TR',
      now(), now(), 0, '${normalizedEmail}', 0
    );
  `);

  try {
    const registerResponse = await request.post(`${apiBaseUrl}/api/customer/v1/auth/register`, {
      headers: { "Accept-Language": "en-US" },
      data: {
        email,
        password: "IgnoredPassword123!",
        fullName: "Ignored Profile Change",
        phone: "+900000000000",
      },
    });
    expect(registerResponse.ok()).toBe(true);

    const queuedPayloadText = runSql(`
      SELECT payload
      FROM background_jobs
      WHERE payload LIKE '%${email}%'
      ORDER BY created_at DESC
      LIMIT 1;
    `);
    expect(queuedPayloadText).not.toBe("");

    const queuedPayload = JSON.parse(queuedPayloadText) as {
      ToEmail: string;
      Locale: string;
      Variables: { ClaimUrl: string };
    };
    expect(queuedPayload.ToEmail).toBe(email);
    expect(queuedPayload.Locale).toBe("en-US");

    const claimUrl = new URL(queuedPayload.Variables.ClaimUrl, "http://localhost");
    const token = claimUrl.searchParams.get("token");
    expect(token).toBeTruthy();

    for (const expectation of localeExpectations) {
      await page.goto(`/${expectation.locale}/account-claim?token=${encodeURIComponent(token!)}`);
      await expect(page.getByRole("heading", { name: expectation.title })).toBeVisible();
      await expect(page.getByRole("button", { name: expectation.submit })).toBeVisible();
    }

    await page.goto(`/en/account-claim?token=${encodeURIComponent(token!)}`);
    await page.getByLabel("New password", { exact: true }).fill(password);
    await page.getByLabel("Confirm new password", { exact: true }).fill(password);
    const claimResponsePromise = page.waitForResponse(
      (response) => response.url().endsWith("/api/customer/v1/auth/claim") && response.request().method() === "POST"
    );
    await page.getByRole("button", { name: "Set Password" }).click();
    const claimResponse = await claimResponsePromise;
    expect(claimResponse.ok()).toBe(true);
    await expect(page.getByText("Your account has been successfully claimed. You can now sign in")).toBeVisible();

    const replayResponse = await request.post(`${apiBaseUrl}/api/customer/v1/auth/claim`, {
      data: { token, newPassword: "ReplayPassword123!" },
    });
    expect(replayResponse.status()).toBe(400);

    const loginResponse = await request.post(`${apiBaseUrl}/api/customer/v1/auth/login`, {
      data: { email, password },
    });
    expect(loginResponse.ok()).toBe(true);

    const persistedState = runSql(`
      SELECT
        (password_hash IS NOT NULL)::text || '|' ||
        full_name || '|' ||
        phone
      FROM customers
      WHERE id = '${customerId}';
    `);
    expect(persistedState).toBe("true|Codex Claim|");
  } finally {
    runSql(`
      DELETE FROM auth_sessions WHERE principal_id = '${customerId}';
      DELETE FROM background_jobs WHERE payload LIKE '%${email}%';
      DELETE FROM customer_account_claim_tokens WHERE customer_id = '${customerId}';
      DELETE FROM audit_logs WHERE entity_id = '${customerId}';
      DELETE FROM customers WHERE id = '${customerId}';
    `);
  }
});
